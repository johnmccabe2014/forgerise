/** @type {import('next').NextConfig} */
const nextConfig = {
  // Required for the slim production container (web/Dockerfile copies .next/standalone).
  output: "standalone",
  reactStrictMode: true,
  poweredByHeader: false,
};

export default nextConfig;
