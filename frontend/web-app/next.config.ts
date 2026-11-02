import type { NextConfig } from "next";

// Alt yol (subpath) dagitimi: prod'da NEXT_PUBLIC_BASE_PATH=/carsties verilir,
// lokal gelistirmede bos kalir (localhost:3000 kokunden calisir).
const basePath = process.env.NEXT_PUBLIC_BASE_PATH || undefined;

const nextConfig: NextConfig = {
  basePath,
  output: 'standalone',
  logging: {
    fetches: {
      fullUrl: true
    }
  },
  images: {
    remotePatterns: [
      {protocol: 'https', hostname: 'cdn.pixabay.com'},
      {protocol: 'https', hostname: 'loremflickr.com'},
    ]
  },
  reactCompiler: true,
};

export default nextConfig;
