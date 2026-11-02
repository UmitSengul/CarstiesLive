import {Card} from "@/components/ui/card";
import {Lock} from "lucide-react";
import LoginButton from "@/components/nav/LoginButton";

// Next.js basePath'i (prod: /carsties) olmadan donus adresi kok dizine (/)
// gider; bu yuzden varsayilan callback basePath ile kurulur.
const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? '';

export default async function SignInPage(props: PageProps<"/auth/sign-in">) {
    const searchParams = await props.searchParams;
    const callback = String(searchParams["callbackUrl"]) || `${basePath}/`;
    
    return (
        <Card className='h-100 w-1/2 mx-auto my-auto flex flex-col gap-3 justify-center items-center text-center'>
            <Lock className='h-28 w-28' />
            <h2 className='text-2xl font-semibold'>You need to sign in to do that</h2>
            <p className='text-muted-foreground'>
                Please click the login button below to continue</p>
            <LoginButton callbackUrl={callback} />
        </Card>
    );
}