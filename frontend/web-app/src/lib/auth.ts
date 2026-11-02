import { betterAuth } from "better-auth";
import {genericOAuth, username} from "better-auth/plugins";
import {headers} from "next/headers";

// better-auth, Next.js'in basePath ayarindan haberdar degildir; ayni degeri
// kendi basePath'i olarak almak zorunda: <basePath>/api/auth
const basePath = `${process.env.NEXT_PUBLIC_BASE_PATH ?? ''}/api/auth`;

// BETTER_AUTH_URL bir origin ise (path'siz) onu trusted origin olarak ekle.
// Path iceren bir deger verilirse better-auth kendi baseURL'unden turetir.
const trustedOrigins = (() => {
    const url = process.env.BETTER_AUTH_URL;
    if (!url) return [];
    try {
        return [new URL(url).origin];
    } catch {
        return [];
    }
})();

export const auth = betterAuth({
    baseURL: process.env.BETTER_AUTH_URL,
    basePath,
    trustedOrigins,
    user: {
        additionalFields: {
            username: {
                type: "string",
                required: true
            }
        }
    },
    plugins: [
        genericOAuth({
            config: [
                {
                    providerId: "duende",
                    clientId: "nextApp",
                    clientSecret: "NotASecret",
                    // better-auth 1.7: generic OAuth artik `issuer` alani kullanmiyor;
                    // endpoint'ler acikca verilir ve akis `signIn.social` uzerinden yurur.
                    authorizationUrl: process.env.NEXT_PUBLIC_ID_URL + '/connect/authorize',
                    tokenUrl: process.env.ID_URL_INTERNAL + '/connect/token',
                    userInfoUrl: process.env.ID_URL_INTERNAL + '/connect/userinfo',
                    scopes: ["openid", "profile", "auctionApp"],
                    pkce: true,
                    prompt: 'login'
                }
            ]
        }),
        username()
    ]
});

export async function getCurrentUser() {
    try {
        const session = await auth.api.getSession({
            headers: await headers()
        });
        
        if (!session) return null;
        
        return session.user;
    } catch (error) {
        console.log(error);
        return null;
    }
}

export type SessionUser = typeof auth.$Infer.Session.user;