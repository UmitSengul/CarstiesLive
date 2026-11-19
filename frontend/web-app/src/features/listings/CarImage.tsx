'use client';

import Image from "next/image";
import {clsx} from "clsx";

type Props = {
    imageUrl: string;
    thumbnail?: boolean;
}

export default function CarImage({ imageUrl, thumbnail = true }: Props) {
    // NOT (2026-11-19): Burada bir "fade-in" vardi: gorsel opacity-0 ile basliyor,
    // yalnizca onLoad ile gorunur oluyordu. Onbellekten gelen gorselde load olayi
    // React handler'i baglanmadan once tetiklendigi icin kart gorselleri ana sayfada
    // hic gorunmuyordu. ref+complete kontrolu de yetmedi (next/image ref'i garanti
    // etmiyor). En dayanikli cozum: gorsel HER ZAMAN gorunur, animasyon yok.
    return (
        <Image
            src={imageUrl}
            alt='Image of car'
            height={thumbnail ? 400 : 1200}
            width={thumbnail ? 400 : 1200}
            className={clsx(
                'w-full object-cover aspect-16/10 border-b border-muted-foreground/20',
                {'rounded-xl': !thumbnail}
            )}
            // Kartlarda 10 gorsel ayni anda preload edilmesin; yalniz detay oncelikli.
            priority={!thumbnail}
            sizes='(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw'
        />
    );
}
