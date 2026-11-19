'use client';

import Image from "next/image";
import {useEffect, useRef, useState} from "react";
import {clsx} from "clsx";

type Props = {
    imageUrl: string;
    thumbnail?: boolean;
}

export default function CarImage({ imageUrl, thumbnail = true }: Props) {
    const [isLoading, setIsLoading] = useState(true);
    const imgRef = useRef<HTMLImageElement>(null);

    // Onbellekten gelen gorselde 'load' olayi React handler'i baglanmadan once
    // tetiklenir; o zaman isLoading true kalir ve gorsel opacity-0'da GORUNMEZ olur.
    // Liste sayfasinda ayni gorseller tekrar ziyarette hep bu duruma dusuyordu
    // (2026-11-19: ana sayfada resimler yuklenmiyor sikayeti).
    useEffect(() => {
        if (imgRef.current?.complete) setIsLoading(false);
    }, []);

    return (
        <Image
            ref={imgRef}
            src={imageUrl}
            alt='Image of car'
            height={thumbnail ? 400 : 1200}
            width={thumbnail ? 400 : 1200}
            className={clsx('object-cover duration-700 ease-in-out aspect-16/10 border-b border-muted-foreground/20', {
                'opacity-0 scale-105': isLoading,
                'opacity-100 scale-100': !isLoading,
                'rounded-xl': !thumbnail
            })}
            // 10 kart ayni anda preload edilmesin; yalniz detay gorseli oncelikli
            priority={!thumbnail}
            sizes='(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw'
            onLoad={() => setIsLoading(false)}
            // Yukleme kalici olarak basarisiz olursa da gorunur kalsin (kirik ikon),
            // sonsuza dek gizli kalmasindan iyidir.
            onError={() => setIsLoading(false)}
        />
    );
}
