import { auth } from "@/lib/auth";
import { toNextJsHandler } from "better-auth/next-js";

const betterAuthHandler = toNextJsHandler(auth);

// Next.js, yapilandirilmis basePath'i (/carsties) req.url'den SILEREK route
// handler'a verir (next/dist/server/base-server.js -> removePathPrefix), oysa
// better-auth kendi basePath'ini tam yol olarak bekler
// (ctx.baseURL = https://<domain>/carsties/api/auth). Bu yuzden handler'a
// verilen istegin yolunu basePath ile yeniden kuruyoruz.
const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? "";

function withBasePath(request: Request): Request {
    if (!basePath) return request;

    const url = new URL(request.url);
    if (!url.pathname.startsWith("/api/auth")) return request;

    url.pathname = `${basePath}${url.pathname}`;

    return new Request(url, {
        method: request.method,
        headers: request.headers,
        body: request.body,
        // Node fetch (undici) streaming govde icin bunu ister
        duplex: "half",
    } as RequestInit);
}

export const GET = (request: Request) =>
    betterAuthHandler.GET(withBasePath(request) as never);

export const POST = (request: Request) =>
    betterAuthHandler.POST(withBasePath(request) as never);
