'use client';

import {useRouter} from "next/navigation";
import {Card} from "@/components/ui/card";
import {CarFront} from "lucide-react";
import {Button} from "@/components/ui/button";

type Props = {
    title?: string;
    subtitle?: string;
    showReset?: boolean;
}

export default function EmptyState({ 
   title = "No matches for this search", 
   subtitle = "Try changing the filters or resetting them", 
   showReset = true 
}: Props) {
    const router = useRouter()
    
    return (
        <Card className='flex w-full max-w-md flex-col items-center justify-center gap-3 p-8 text-center sm:p-10'>
            <CarFront className='h-28 w-28' />
            <h2 className='text-2xl font-semibold'>{title}</h2>
            <p className='text-muted-foreground'>{subtitle}</p>
            {showReset && (
                <Button onClick={() => router.push('/')} className='mt-2'>
                    Reset filters 
                </Button>
            )}
        </Card>
    );
}