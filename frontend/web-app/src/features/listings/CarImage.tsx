'use client';

import Image from "next/image";
import {useState} from "react";
import {clsx} from "clsx";

type Props = {
    imageUrl: string;
    thumbnail?: boolean;
}

export default function CarImage({ imageUrl, thumbnail = true }: Props) {
    const [isLoading, setIsLoading] = useState(true);
    
    return (
        <Image
            src={imageUrl}
            alt='Image of car'
            height={thumbnail ? 400 : 1200}
            width={thumbnail ? 400 : 1200}
            sizes={thumbnail
                ? '(max-width: 640px) 100vw, (max-width: 1024px) 50vw, (max-width: 1280px) 33vw, 25vw'
                : '(max-width: 1024px) 100vw, 50vw'}
            className={clsx('h-auto w-full object-cover duration-700 ease-in-out aspect-16/10 border-b border-muted-foreground/20', {
                'opacity-0 scale-105': isLoading,
                'opacity-100 scale-100': !isLoading,
                'rounded-xl': !thumbnail
            })}
            priority
            onLoad={() => setIsLoading(false)}
        />
    );
}