'use client';

import {Button} from "@/components/ui/button";
import {authClient} from "@/lib/auth-client";

type Props = {
    callbackUrl?: string;
}

// better-auth callbackURL'i baseURL'e gore cozumler; basePath'i elle eklemek gerekir.
const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? '';

export default function LoginButton({ callbackUrl = `${basePath}/` }: Props) {
    return (
        <Button
            variant='outline'
            size='lg'
            onClick={() => authClient.signIn.social({
                provider: 'duende',
                callbackURL: callbackUrl
            })}
        >
            Login
        </Button>
    );
}
