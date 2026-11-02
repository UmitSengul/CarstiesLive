import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'
import {getSessionCookie} from "better-auth/cookies";

// Next.js basePath'i (prod: /carsties) request.url'e dahildir; bu yuzden
// yonlendirme hedeflerini basePath ile birlikte kurmak gerekir.
const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? '';

// This function can be marked `async` if using `await` inside
export function proxy(request: NextRequest) {
    const sessionCookie = getSessionCookie(request);
    
    if (!sessionCookie) {
        const currentPath = request.nextUrl.pathname.startsWith(basePath)
            ? request.nextUrl.pathname
            : `${basePath}${request.nextUrl.pathname}`;

        const signInUrl = new URL(`${basePath}/auth/sign-in`, request.url);
        signInUrl.searchParams.set("callbackUrl", 
            currentPath + request.nextUrl.search);
        
        return NextResponse.redirect(signInUrl);
    }
    
    return NextResponse.next();
}

export const config = {
    matcher: ["/session"],
}