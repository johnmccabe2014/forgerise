# ForgeRise — Video Intelligence Architecture

**Status:** Draft (design only — no code shipped yet)
**Author:** Architecture working note
**Last updated:** 2026-05-10

This document describes the Video Intelligence module that lives **inside** the
ForgeRise platform. It is not a separate product. Its job is to convert messy
grassroots match and training footage into a small number of high-signal
coaching outputs (summary, highlight clips, next-session suggestions, voice-note
context) with as little coach effort as possible.

It is intentionally designed so that the heavy parts (encoding, AI inference)
can be lifted out into a separate service later without rewriting the API or
the data model.

---

## 1. Architecture overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ForgeRise Web (Next.js)                          │
│   - Upload UI, voice-note recorder, timeline, clip review                   │
│   - Server actions / proxy → ForgeRise.Api                                  │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │  HTTPS, cookie auth
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ForgeRise.Api (.NET 9)                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Existing modules: Auth, Teams, Players, Sessions, Welfare, Drills…  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Video Intelligence module (new)                                     │   │
│  │   • Endpoints:  /teams/{teamId}/videos/...                          │   │
│  │   • Services:   IUploadService, ITimelineService, IClipService,     │   │
│  │                 ITranscriptService, IInsightService                 │   │
│  │   • Repos:      EF Core entities listed in §3                       │   │
│  │   • Outbox:     publishes work items to the queue                   │   │
│  └────────────────────────────┬─────────────────────────────────────────┘   │
└───────────────────────────────┼─────────────────────────────────────────────┘
                                │ enqueue (event-driven)
                                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ForgeRise.VideoWorker (.NET 9)                       │
│  Background service consuming the queue. Stages:                            │
│  ① validate → ② probe (ffprobe) → ③ encode previews/proxies (ffmpeg)        │
│  ④ extract audio → ⑤ transcribe → ⑥ AI summarise → ⑦ propose highlights    │
│  ⑧ persist results → ⑨ notify API (webhook back / direct DB write)          │
└──────────────┬─────────────────────────────────┬────────────────────────────┘
               │                                 │
               ▼                                 ▼
   ┌────────────────────────┐         ┌─────────────────────────┐
   │  Object Storage        │         │  AI providers           │
   │  IObjectStore          │         │  ITranscriber           │
   │  - LocalFs (dev)       │         │  ISummariser            │
   │  - S3 / Azure Blob     │         │  IVisionAnalyser (later)│
   │  - signed URLs         │         └─────────────────────────┘
   └────────────────────────┘
```

**Key boundaries:**

- The **API** owns all data and authorisation. It never blocks on encoding or
  AI calls — it writes a row, enqueues a job, and returns 202.
- The **Worker** is a separate process so we can scale it independently and
  later run it on GPU nodes. In MVP it runs as a sidecar deployment in k3s.
- **Storage** and **AI** are behind interfaces so we can swap LocalFs → S3 and
  swap a stub transcriber → Whisper API → self-hosted Whisper without
  touching call sites.

---

## 2. Folder structure

New top-level additions (existing folders unchanged):

```
api/
  ForgeRise.Api/
    Features/
      Video/
        Endpoints/
          UploadsController.cs
          VideosController.cs
          ClipsController.cs
          TimelineController.cs
          VoiceNotesController.cs
          InsightsController.cs
        Services/
          IUploadService.cs / UploadService.cs
          IClipService.cs    / ClipService.cs
          ITimelineService.cs / TimelineService.cs
          IInsightService.cs  / InsightService.cs
        Storage/
          IObjectStore.cs
          LocalFsObjectStore.cs
          S3ObjectStore.cs              # later
        Queue/
          IVideoJobQueue.cs
          OutboxVideoJobQueue.cs
        Dtos/
        Validators/
    Data/
      Entities/Video/
        VideoAsset.cs
        VideoUploadSession.cs
        VideoClip.cs
        VideoTag.cs
        VideoTimelineEvent.cs
        CoachVoiceNote.cs
        TranscriptSegment.cs
        AiVideoInsight.cs
        HighlightCandidate.cs
        SessionVideoLink.cs
      Migrations/...
  ForgeRise.VideoWorker/                # new project
    Program.cs
    Pipeline/
      IPipelineStage.cs
      ProbeStage.cs
      EncodeStage.cs
      AudioExtractStage.cs
      TranscribeStage.cs
      SummariseStage.cs
      HighlightProposeStage.cs
    Providers/
      IFfmpegRunner.cs
      ITranscriber.cs / WhisperApiTranscriber.cs / StubTranscriber.cs
      ISummariser.cs  / OpenAiSummariser.cs / StubSummariser.cs
    Workers/
      VideoJobWorker.cs                  # IHostedService
  ForgeRise.Api.Tests/
    Video/...
  ForgeRise.VideoWorker.Tests/

web/
  src/
    app/teams/[teamId]/video/
      page.tsx                           # list of session videos
      upload/page.tsx                    # upload + voice note
      [videoId]/page.tsx                 # timeline + AI summary + clips
    components/video/
      VideoUploader.tsx
      VoiceNoteRecorder.tsx
      VideoTimeline.tsx
      ClipList.tsx
      InsightPanel.tsx

infra/
  k3s/video-worker.yaml                  # Deployment + PVC for /tmp work area
  docker-compose.video.yml               # dev: api + worker + minio
  helm/forgerise/templates/video-worker.yaml  # if/when we move to Helm
```

---

## 3. Database schema proposal

EF Core, PostgreSQL prod / InMemory tests (existing convention). All entities
are scoped to `TeamId` and gated by `TeamScope.RequireOwnedTeam`.

### `VideoUploadSession`
Multi-part / chunked upload bookkeeping. Lets us resume a partial upload from
a phone on a flaky pitch-side connection.

| Column            | Type                | Notes                              |
|-------------------|---------------------|------------------------------------|
| Id                | Guid PK             |                                    |
| TeamId            | Guid FK             | scope                              |
| InitiatedByUserId | Guid FK             |                                    |
| OriginalFilename  | text                |                                    |
| MimeType          | text                | validated against allow-list       |
| TotalBytes        | bigint              |                                    |
| BytesReceived     | bigint              |                                    |
| Status            | text enum           | `pending|uploading|complete|failed`|
| StorageKey        | text                | object-store key for the raw file  |
| CreatedAt         | timestamptz         |                                    |
| CompletedAt       | timestamptz null    |                                    |

### `VideoAsset`
The canonical row representing one piece of footage post-upload.

| Column           | Type           | Notes                                          |
|------------------|----------------|------------------------------------------------|
| Id               | Guid PK        |                                                |
| TeamId           | Guid FK        |                                                |
| UploadSessionId  | Guid FK        |                                                |
| OwnerUserId      | Guid FK        | uploader                                       |
| Kind             | text enum      | `match|training|drill|other`                   |
| Title            | text           | coach-set, defaults to filename                |
| RecordedAt       | timestamptz?   | best-effort from EXIF/coach-set                |
| DurationSeconds  | int            | filled by ProbeStage                           |
| Width / Height   | int            |                                                |
| Fps              | numeric(5,2)   |                                                |
| RawStorageKey    | text           | original                                       |
| ProxyStorageKey  | text?          | 720p H.264 for cheap streaming                 |
| ThumbStorageKey  | text?          |                                                |
| ProcessingState  | text enum      | `queued|probing|encoding|transcribing|summarising|ready|failed` |
| ProcessingError  | text?          |                                                |
| Visibility       | text enum      | `team|coaches_only` (default coaches_only)     |
| CreatedAt        | timestamptz    |                                                |
| UpdatedAt        | timestamptz    |                                                |

### `SessionVideoLink`
Many-to-many between video assets and `Session` rows (existing entity).

| VideoAssetId | Guid FK | (composite PK) |
| SessionId    | Guid FK | (composite PK) |
| LinkedAt     | timestamptz |             |

### `VideoTimelineEvent`
A coach-authored marker on the timeline. The “timestamp note”.

| Id, TeamId, VideoAssetId, AuthorUserId, OffsetSeconds, DurationSeconds?, Kind (`note|highlight|drill|welfare_flag`), Label, Notes, CreatedAt |

`welfare_flag` events are stored but their `Notes` are scrubbed from any
non-welfare-permission viewer per master prompt §9.

### `VideoClip`
A persisted clip cut from the parent asset.

| Id, TeamId, VideoAssetId, StartSeconds, EndSeconds, Title, ProxyStorageKey, GeneratedBy (`coach|ai`), TimelineEventId? (when cut from a marker), CreatedAt |

### `VideoTag`
Lightweight free-form or controlled tags. M:N to `VideoAsset` via `VideoAssetTag`.

| Id, TeamId, Name, Slug, Category (`tactic|skill|player|opposition|moment|other`) |

### `CoachVoiceNote`
Coach voice memo — may exist on its own (post-match) or attached to a video at
an offset.

| Id, TeamId, AuthorUserId, VideoAssetId? (nullable for standalone notes), OffsetSeconds?, DurationSeconds, AudioStorageKey, ProcessingState (`queued|transcribing|ready|failed`), CreatedAt |

### `TranscriptSegment`
Output of transcription for either a video's audio or a voice note.

| Id, TeamId, SourceKind (`video|voice_note`), SourceId, StartSeconds, EndSeconds, Text, Confidence (numeric), Speaker? |

### `AiVideoInsight`
Generated coaching summary / themes / next-session suggestions.

| Id, TeamId, VideoAssetId, Kind (`summary|themes|next_session|safety_note`), Body (jsonb), Confidence (numeric), ModelTag (text), CreatedAt |

`Body` is jsonb so we can evolve schemas without migrations.

### `HighlightCandidate`
Proposed highlight that has not yet been promoted to a `VideoClip`.

| Id, TeamId, VideoAssetId, StartSeconds, EndSeconds, Reason (text), Score (numeric 0-1), Source (`coach_voice|transcript|cv` later), Status (`proposed|accepted|rejected`), DecidedByUserId?, DecidedAt? |

**Indexes (always include `TeamId` first per existing convention):**
`(TeamId, VideoAssetId, OffsetSeconds)` on TimelineEvent;
`(TeamId, ProcessingState, CreatedAt)` on VideoAsset;
`(TeamId, SourceKind, SourceId, StartSeconds)` on TranscriptSegment.

---

## 4. Service boundaries

| Service              | Owns                                          | Talks to                  |
|----------------------|-----------------------------------------------|---------------------------|
| `IUploadService`     | upload sessions, MIME/size guards, completion | `IObjectStore`, queue     |
| `IClipService`       | clip CRUD, on-demand cut requests             | `IObjectStore`, queue     |
| `ITimelineService`   | timeline events + welfare scrubbing           | EF Core only              |
| `ITranscriptService` | transcript reads, voice-note assembly         | EF Core only              |
| `IInsightService`    | reads insights, exposes summary/themes        | EF Core only              |
| Worker pipeline      | mutation of `ProcessingState`, transcripts, insights | DB direct, providers |

The API never calls ffmpeg or the AI providers directly. The Worker never
exposes HTTP. Communication is purely via the queue and the shared DB.

---

## 5. API contracts

All endpoints sit under `/teams/{teamId}/videos` and require team scope.
Versioned under `/v1/`.

```
POST   /v1/teams/{teamId}/videos/uploads             → 201 { uploadSessionId, uploadUrl, partSize }
PUT    /v1/teams/{teamId}/videos/uploads/{id}/parts/{n}   (binary chunk)
POST   /v1/teams/{teamId}/videos/uploads/{id}/complete    → 202 { videoAssetId, processingState: "queued" }

GET    /v1/teams/{teamId}/videos                     ?kind=&state=&q=&page=
GET    /v1/teams/{teamId}/videos/{videoId}           → asset + processingState + counts
GET    /v1/teams/{teamId}/videos/{videoId}/status    → cheap polling endpoint
GET    /v1/teams/{teamId}/videos/{videoId}/stream    → 302 to short-lived signed URL (proxy mp4)
GET    /v1/teams/{teamId}/videos/{videoId}/thumbnail → 302 signed URL

GET    /v1/teams/{teamId}/videos/{videoId}/timeline
POST   /v1/teams/{teamId}/videos/{videoId}/timeline             { offsetSec, kind, label, notes? }
PATCH  /v1/teams/{teamId}/videos/{videoId}/timeline/{eventId}
DELETE /v1/teams/{teamId}/videos/{videoId}/timeline/{eventId}

POST   /v1/teams/{teamId}/videos/{videoId}/voice-notes          (multipart audio)
GET    /v1/teams/{teamId}/videos/{videoId}/transcript

GET    /v1/teams/{teamId}/videos/{videoId}/insights              → summary + themes + next-session
GET    /v1/teams/{teamId}/videos/{videoId}/highlights            → proposed + accepted
POST   /v1/teams/{teamId}/videos/{videoId}/highlights/{id}/accept   → creates VideoClip
POST   /v1/teams/{teamId}/videos/{videoId}/highlights/{id}/reject

POST   /v1/teams/{teamId}/videos/{videoId}/clips                 { startSec, endSec, title } → 202
GET    /v1/teams/{teamId}/videos/{videoId}/clips
GET    /v1/teams/{teamId}/clips/{clipId}/stream                  → 302 signed URL
```

DTOs are PascalCase records living in `Features/Video/Dtos/`. All long-running
ops return `202 Accepted` plus a status URL. Clients poll `…/status` (cheap
read of `ProcessingState`); we can add SSE later.

---

## 6. Queue / worker architecture

**MVP queue:** an EF Core "outbox" table (`VideoJob`) plus a polling worker.
Zero ops cost, runs in dev with no extra services. Schema:

| Id | TeamId | VideoAssetId | JobType (`probe|encode|transcribe|summarise|cut_clip`) | Payload (jsonb) | State (`queued|in_progress|done|failed`) | Attempts | NextAttemptAt | LockedBy | LockedUntil | CreatedAt | LastError? |

Worker loop: `SELECT … FOR UPDATE SKIP LOCKED LIMIT N WHERE State='queued' AND NextAttemptAt <= now()`. Exponential backoff with jitter on failure, capped at 5
attempts, then `failed`.

**Why not Redis/RabbitMQ in MVP?** It would mean a new component to deploy,
secure, monitor, and back up. Postgres SKIP LOCKED is good for thousands of
jobs/min, far above grassroots scale, and we can swap in MassTransit + Rabbit
later behind `IVideoJobQueue` without touching pipeline stages.

**Pipeline stages** (`IPipelineStage<TInput, TOutput>`): each stage is pure-ish
— takes the asset + working dir + providers, mutates state, returns next job
to enqueue. Stages are independently retryable and have explicit timeouts.

---

## 7. Video processing pipeline

```
upload complete
   │
   ▼
[Probe]      ffprobe → duration, resolution, fps, codec
   │
   ▼
[Encode]     ffmpeg → 720p H.264 proxy (CRF 26) + thumbnail.jpg @ duration*0.1
   │
   ▼
[ExtractAudio]  ffmpeg → 16kHz mono WAV (for transcription)
   │
   ▼
[Transcribe]    ITranscriber → TranscriptSegment rows
   │             (phase 1: stub returns voice notes only)
   │             (phase 2: Whisper API — small/base model)
   │             (phase 3: self-hosted whisper.cpp on GPU node)
   ▼
[Summarise]     ISummariser → AiVideoInsight rows (kind=summary,themes,next_session)
   │             input = transcript + linked session metadata + welfare-safe drills
   │
   ▼
[ProposeHighlights]   heuristics over voice notes + transcript verbs
   │                  ("great line break", "huge tackle", "carry, carry, carry")
   │                  → HighlightCandidate rows
   ▼
asset.ProcessingState = ready
```

Future stages plug in after `Encode` (vision):
`[Detect: tackles/rucks/linebreaks]` → enrich `HighlightCandidate.Source='cv'`
without changing the API contract.

Working area: a per-job tempdir under `/var/forgerise/video-work/{jobId}/`,
deleted on success. PVC sized at e.g. 50 GiB in k3s.

---

## 8. Local development workflow

Add `infra/docker-compose.video.yml`:

```
services:
  api:           # existing
  postgres:      # existing
  minio:         # S3-compatible object store for dev
    image: minio/minio:RELEASE.2024-...
    command: server /data --console-address ":9001"
    ports: ["9000:9000", "9001:9001"]
    environment:
      MINIO_ROOT_USER: forgerise
      MINIO_ROOT_PASSWORD: forgerise-dev
  video-worker:
    build: { context: .., dockerfile: api/ForgeRise.VideoWorker/Dockerfile }
    environment:
      ConnectionStrings__Default: "Host=postgres;..."
      ObjectStore__Provider: "s3"
      ObjectStore__Endpoint: "http://minio:9000"
      Transcriber__Provider: "stub"   # no external API in dev by default
      Summariser__Provider: "stub"
    depends_on: [postgres, minio]
```

Coach can run `pnpm dev` + `dotnet run` + `docker compose -f infra/docker-compose.video.yml up minio video-worker` and exercise the full pipeline locally with stub AI providers.

A `samples/` folder ships ~5 short Creative-Commons rugby clips (or generated
solid-colour mp4s) so the worker has something to chew on without us shipping
a 500MB asset to git.

---

## 9. GitHub Actions workflows

New jobs in the existing `ci.yml`:

```
jobs:
  api-tests:        # existing — extend to also test ForgeRise.VideoWorker.Tests
  worker-build:
    runs-on: ubuntu-latest
    steps:
      - dotnet build api/ForgeRise.VideoWorker
      - dotnet test  api/ForgeRise.VideoWorker.Tests
  worker-image:
    needs: worker-build
    steps:
      - docker buildx build … -t ghcr.io/forgerise/video-worker:${{ sha }}
      - trivy image ghcr.io/forgerise/video-worker:${{ sha }}        # image scan
      - syft packages dir:./api/ForgeRise.VideoWorker -o cyclonedx-json > sbom.json
      - upload SBOM artifact
```

Existing `security.yml` (CodeQL, dep scanning) automatically picks up the new
project. The existing `deploy.yml` gets a third `kubectl rollout status …
deployment/forgerise-video-worker` after the api/web ones, with the same
smoke-probe pattern as the recent deploy fix.

---

## 10. k3s deployment structure

`infra/k3s/video-worker.yaml`:

- `Deployment forgerise-video-worker` — single replica MVP, `imagePullPolicy: Always`, env from `forgerise-secrets` and `forgerise-config` (already exist).
- `PersistentVolumeClaim video-worker-work` — 50Gi `local-path` for tempdirs.
- `ServiceAccount forgerise-video-worker` — minimal RBAC, no cluster perms.
- No Service / no Ingress (worker is internal).
- `livenessProbe`/`readinessProbe` on a tiny /healthz exposed only on `127.0.0.1`.
- `resources.requests`: 500m CPU / 1Gi RAM; `limits`: 4 CPU / 4Gi (ffmpeg burst).

Object storage: dev uses MinIO in-cluster; prod uses MinIO bucket on the
self-hosted box for now, abstraction allows swap to S3 / Azure Blob later.

GPU: when we add vision, add a second deployment `forgerise-video-gpu-worker`
with `nvidia.com/gpu: 1` and a node selector. No code change in the API.

---

## 11. Security considerations

| Concern | Mitigation |
|---|---|
| Anonymous video access | All read endpoints require team membership; raw URLs are signed and short-lived (15m). No `public-read` ACLs. |
| Upload abuse / DoS | Per-team daily byte quota; per-request 4GiB hard cap; Content-Length verified pre-upload; chunked PUTs require valid `uploadSessionId` issued seconds earlier. |
| MIME spoofing | MIME allow-list (`video/mp4`, `video/quicktime`, `video/webm`) checked **after** ffprobe — header sniff, not extension. |
| Malware in container payloads | Hook point `IFileScanner` runs after upload-complete, before queueing Probe; MVP impl is a no-op that logs "scan_skipped"; later swap in ClamAV sidecar. |
| Sensitive welfare data leakage | Timeline events with `Kind=welfare_flag` have `Notes` scrubbed for non-welfare-permission viewers; AI prompts are constructed by `IInsightService` which never includes raw welfare detail (master prompt §9). |
| Voice notes contain identifying info | Stored encrypted at rest (object store SSE); transcripts include speaker labels only when the team has explicitly opted in. |
| Signed URL leakage | URLs signed with HMAC of `(assetId, expiresAt, viewerUserId)`; replay outside the window fails closed. |
| Worker compromise → full DB access | Worker uses a separate PG role with `INSERT/UPDATE` only on video.* tables and `SELECT` on the few entities it needs (Session, Drill). No DELETE. |
| Cost runaway on AI providers | `IInsightService` has a per-team monthly token budget; over budget → degrade to themes-only summary (no model call). Budget surfaced in admin UI. |

---

## 12. Observability plan

- **OpenTelemetry** SDK in both API and Worker. Resource attrs: `service.name`,
  `service.version`, `forgerise.team_id` (per-request), `forgerise.video_asset_id`.
- **Traces**: every pipeline stage is a span; root trace = upload-complete →
  ready. Cross-process via the queue payload carrying `traceparent`.
- **Metrics**:
  - `video.uploads.bytes` counter
  - `video.processing.duration{stage}` histogram
  - `video.queue.depth` gauge
  - `video.queue.lag_seconds` gauge
  - `ai.cost.tokens{provider,kind}` counter
  - `video.assets.failed_total{stage,error_class}` counter
- **Logs**: structured JSON, `correlationId = traceId`, `teamId`, `videoAssetId`,
  `jobId`, `stage`. Welfare notes never logged.
- **Dashboards**: a Grafana board with: queue depth, p95 stage duration,
  failure rate by stage, AI spend per team per day.
- **Alerts**: `video.queue.lag_seconds > 300 for 10m`,
  `video.assets.failed_total rate > 5/h`, `ai.cost.tokens > 80% of monthly cap`.

---

## 13. MVP implementation roadmap

Sliced so each step is independently shippable behind a feature flag
(`features.video=true`). Suggested order:

| # | Slice | What lands |
|---|---|---|
| V1 | **Skeleton + entities** | new project, EF entities, migration, empty controllers returning 501 behind `features.video`. CI green. |
| V2 | **Direct upload (small files)** | single-shot `POST /videos` (≤200MB), `IObjectStore.LocalFs`, `VideoAsset` row, no processing. UI: minimal upload page. |
| V3 | **Probe + encode + thumbnail** | worker project, ffmpeg in image, in-process queue, ProcessingState transitions, stream endpoint serving the proxy. |
| V4 | **Coach timeline notes** | timeline endpoints + UI overlay on the player. No AI yet — proves value alone. |
| V5 | **Voice notes + transcripts (stub)** | upload voice note, store, fake transcript so the UI can be built. |
| V6 | **Real transcription** | swap stub → Whisper API. Pipeline-only change. |
| V7 | **AI summary + next-session** | `ISummariser` w/ welfare-scrubbed prompt; insights panel. |
| V8 | **Heuristic highlight candidates** | regex/keyword scoring over transcripts + voice notes; accept-to-clip flow. |
| V9 | **Chunked / resumable upload** | replace V2 single-shot with the multi-part API in §5. |
| V10| **Quotas, signed URLs, MinIO in prod** | hardening before any real teams use it. |

Stop points are explicit: we can ship V4 alone and call it a useful product
("video with notes"). Everything above is additive.

---

## 14. Future scalability roadmap

| Horizon | Capability | What changes |
|---|---|---|
| 6–12mo | GPU worker for self-hosted Whisper + tackle/ruck detection | new deployment + node pool; `IVisionAnalyser` interface; `HighlightCandidate.Source='cv'`. API unchanged. |
| 6–12mo | S3-compatible cloud bucket | swap `IObjectStore` impl. Migration job copies existing keys. |
| 12mo+  | Multi-camera sync | new `VideoAssetGroup` entity referencing N assets with offsets; player UI overlays. |
| 12mo+  | Player-tracking inference | new `PlayerTrack` table; computed by GPU worker; consent required at team level. |
| 12mo+  | Standalone extraction | The Worker already lives in its own process and only talks to DB + queue + storage. To extract: give it its own DB schema (`video.*` is already namespaced), put MassTransit behind `IVideoJobQueue`, replace direct DB writes with REST callbacks to API. ~1-2 weeks of work, not a rewrite. |
| 12mo+  | Real-time live tagging during a match | add `IngestService` that accepts RTMP/SRT, segments to HLS, runs the same pipeline on rolling windows. |

---

## Open questions to resolve before V1

1. Confirm self-hosted box has ffmpeg-friendly disk headroom (≥100Gi free).
2. Pick the dev object store: MinIO in-cluster vs. just LocalFs path-mounted PVC. (MinIO recommended — exercises the signed-URL code path.)
3. Confirm AI provider for V6/V7 — OpenAI vs Azure OpenAI vs self-hosted. Drives cost model and `ISummariser` impl.
4. Welfare opt-in copy — needs a pass from the welfare/legal track before V5 ships.
