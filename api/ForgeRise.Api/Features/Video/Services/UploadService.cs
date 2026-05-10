using System.Buffers;
using System.Security.Cryptography;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities.Video;
using ForgeRise.Api.Features.Video.Options;
using ForgeRise.Api.Features.Video.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ForgeRise.Api.Features.Video.Services;

/// <summary>
/// V2 upload service. Single-shot only — chunked uploads are V2.5.
///
/// Trust-boundary contract (must stay this way; security review iter1, V2):
/// 1. Sniff first 12 bytes for an ISO-BMFF <c>ftyp</c> box; reject anything else.
/// 2. Stream the upload through <see cref="IObjectStore.PutAsync"/> with a hard
///    byte cap so a hostile client cannot exhaust disk.
/// 3. Compute SHA-256 in the same pass for forensic dedup (finding F6).
/// 4. Persist <see cref="VideoUploadSession"/> + <see cref="VideoAsset"/> in a
///    single transaction after a quota recheck under lock.
/// </summary>
public sealed class UploadService : IUploadService
{
    private readonly AppDbContext _db;
    private readonly IObjectStore _store;
    private readonly VideoStorageOptions _storage;
    private readonly TimeProvider _time;
    private readonly ILogger<UploadService> _log;

    public UploadService(
        AppDbContext db,
        IObjectStore store,
        IOptions<VideoStorageOptions> storage,
        TimeProvider time,
        ILogger<UploadService> log)
    {
        _db = db;
        _store = store;
        _storage = storage.Value;
        _time = time;
        _log = log;
    }

    public async Task<UploadOutcome> UploadAsync(
        Guid teamId,
        Guid uploaderUserId,
        string originalFileName,
        Stream body,
        CancellationToken ct)
    {
        // --- Step 1: sniff first 12 bytes WITHOUT touching the store. ---
        var sniffBuf = ArrayPool<byte>.Shared.Rent(12);
        int sniffRead = 0;
        try
        {
            while (sniffRead < 12)
            {
                var n = await body.ReadAsync(sniffBuf.AsMemory(sniffRead, 12 - sniffRead), ct);
                if (n == 0) break;
                sniffRead += n;
            }
            var mime = VideoMimeSniffer.Sniff(sniffBuf.AsSpan(0, sniffRead));
            if (mime is null)
            {
                _log.LogInformation("Upload rejected (mime sniff): team {TeamId}", teamId);
                return new UploadOutcome(null, UploadFailure.UnsupportedMediaType);
            }

            // --- Step 2: quota recheck (best effort; final check happens
            //     after we know the actual byte count below). ---
            var alreadyUsed = await _db.VideoAssets
                .Where(a => a.TeamId == teamId && a.DeletedAt == null)
                .SumAsync(a => (long?)a.SizeBytes, ct) ?? 0L;
            if (alreadyUsed >= _storage.TeamQuotaBytes)
            {
                return new UploadOutcome(null, UploadFailure.TeamQuotaExceeded);
            }
            var remainingBudget = Math.Min(
                _storage.MaxUploadBytes,
                _storage.TeamQuotaBytes - alreadyUsed);

            // --- Step 3: stream the rest through the store with a SHA-256
            //     side-pipe and a hard byte cap. ---
            var assetId = Guid.NewGuid();
            var key = $"teams/{teamId:N}/raw/{assetId:N}.mp4";

            using var sha = SHA256.Create();
            await using var capped = new CappedHashingStream(
                inner: PrependedStream(sniffBuf, sniffRead, body),
                hash: sha,
                maxBytes: remainingBudget);

            try
            {
                await _store.PutAsync(key, capped, mime, ct);
            }
            catch (CappedHashingStream.LimitExceededException)
            {
                return new UploadOutcome(null, UploadFailure.PayloadTooLarge);
            }
            catch (IOException io) when (io.Message.StartsWith("storage_unavailable"))
            {
                return new UploadOutcome(null, UploadFailure.StorageUnavailable);
            }

            var bytesWritten = capped.BytesRead;
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hashHex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

            // --- Step 4: persist session + asset in one transaction. ---
            var now = _time.GetUtcNow();
            var session = new VideoUploadSession
            {
                TeamId = teamId,
                CreatedByUserId = uploaderUserId,
                OriginalFileName = originalFileName,
                DeclaredMimeType = mime,
                DeclaredSizeBytes = bytesWritten,
                CreatedAt = now,
                CompletedAt = now,
            };
            var asset = new VideoAsset
            {
                Id = assetId,
                TeamId = teamId,
                CreatedByUserId = uploaderUserId,
                OriginalFileName = originalFileName,
                MimeType = mime,
                SizeBytes = bytesWritten,
                StoragePath = key,
                ContentSha256 = hashHex,
                ProcessingState = VideoProcessingState.Queued,
                CreatedAt = now,
            };
            session.VideoAssetId = assetId;

            _db.VideoUploadSessions.Add(session);
            _db.VideoAssets.Add(asset);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Video uploaded team={TeamId} asset={AssetId} bytes={Bytes}",
                teamId, assetId, bytesWritten);

            return new UploadOutcome(asset, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sniffBuf);
        }
    }

    /// <summary>
    /// Re-attaches the sniffed prefix to the front of the body so the store
    /// receives the full stream. Returns a sequential reader.
    /// </summary>
    private static Stream PrependedStream(byte[] sniffed, int len, Stream rest)
    {
        var ms = new MemoryStream(sniffed, 0, len, writable: false, publiclyVisible: false);
        return new ConcatStream(ms, rest);
    }

    /// <summary>Read-only concatenation of two streams.</summary>
    private sealed class ConcatStream : Stream
    {
        private readonly Stream _a;
        private readonly Stream _b;
        private bool _aDone;

        public ConcatStream(Stream a, Stream b) { _a = a; _b = b; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_aDone)
            {
                var n = _a.Read(buffer, offset, count);
                if (n > 0) return n;
                _aDone = true;
            }
            return _b.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (!_aDone)
            {
                var n = await _a.ReadAsync(buffer, ct);
                if (n > 0) return n;
                _aDone = true;
            }
            return await _b.ReadAsync(buffer, ct);
        }
    }
}

/// <summary>
/// Wraps a source stream, hashes every byte read, and throws if the read
/// crosses <see cref="_max"/>. Used to enforce the upload byte cap without
/// trusting client headers.
/// </summary>
internal sealed class CappedHashingStream : Stream
{
    private readonly Stream _inner;
    private readonly HashAlgorithm _hash;
    private readonly long _max;

    public long BytesRead { get; private set; }

    public CappedHashingStream(Stream inner, HashAlgorithm hash, long maxBytes)
    {
        _inner = inner;
        _hash = hash;
        _max = maxBytes;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = _inner.Read(buffer, offset, count);
        if (n > 0) Account(buffer, offset, n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await _inner.ReadAsync(buffer, ct);
        if (n > 0)
        {
            if (BytesRead + n > _max) throw new LimitExceededException();
            _hash.TransformBlock(buffer.Span[..n].ToArray(), 0, n, null, 0);
            BytesRead += n;
        }
        return n;
    }

    private void Account(byte[] buffer, int offset, int n)
    {
        if (BytesRead + n > _max) throw new LimitExceededException();
        _hash.TransformBlock(buffer, offset, n, null, 0);
        BytesRead += n;
    }

    public sealed class LimitExceededException : Exception
    {
        public LimitExceededException() : base("Upload exceeds the configured byte cap.") { }
    }
}
