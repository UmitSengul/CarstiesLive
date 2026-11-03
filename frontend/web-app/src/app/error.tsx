'use client'

import { useEffect } from 'react'
import {Card, CardContent, CardFooter, CardHeader} from "@/components/ui/card";
import {Bug} from "lucide-react";
import {Button} from "@/components/ui/button";

export default function ErrorPage({
      error,
      retry,
  }: {
    error: Error & { digest?: string }
    retry: () => void
}) {
    useEffect(() => {
        // Log the error to an error reporting service
        console.error(error)
    }, [error])

    return (
        <Card className='mx-auto my-auto flex w-full max-w-lg flex-col items-center justify-center gap-3 px-4 py-8 sm:py-10'>
            <CardHeader className='flex items-center justify-center'>
                <div className='flex flex-col items-center gap-2'>
                    <Bug className='h-20 w-20 sm:h-32 sm:w-32' />
                    <h2 className='text-2xl font-semibold text-destructive sm:text-3xl'>
                        Server error!
                    </h2>
                </div>
            </CardHeader>
            <CardContent>
                <div className='flex justify-center text-center text-base text-muted-foreground sm:text-xl'>
                    {process.env.NODE_ENV === 'development' 
                        ? error.message : 'An unexpected error occurred.'}
                </div>
            </CardContent>
            <CardFooter>
                <Button
                    onClick={
                        () => retry()
                    }
                >
                    Try again
                </Button>
            </CardFooter>
            
        </Card>
    )
}