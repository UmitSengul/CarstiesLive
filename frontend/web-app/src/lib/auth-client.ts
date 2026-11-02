import { createAuthClient } from "better-auth/react"

// Sunucu tarafindaki betterAuth({basePath}) ile ayni olmali (bkz. src/lib/auth.ts)
const basePath = `${process.env.NEXT_PUBLIC_BASE_PATH ?? ''}/api/auth`;

// better-auth 1.7: generic OAuth saglayicilari `signIn.social` ile kullanilir,
// ayri bir client plugin'i (eski genericOAuthClient) artik gerekmiyor.
export const authClient = createAuthClient({ 
    basePath
})