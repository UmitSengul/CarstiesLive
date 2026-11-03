import {Card, CardContent} from "@/components/ui/card";
import Link from "next/link";
import CountdownTimer from "@/features/listings/CountdownTimer";
import CarImage from "@/features/listings/CarImage";
import {Auction} from "@/lib/types";
import {Badge} from "@/components/ui/badge";
import {User} from "lucide-react";
import CurrentHighBidBadge from "@/features/listings/CurrentHighBidBadge";

type Props = {
    auction: Auction;
}

export default function AuctionCard({ auction }: Props) {
    return (
        <Link href={`/listings/${auction.id}`} 
              className='block min-w-0 transition-transform duration-200 hover:-translate-y-1'>
            <Card className='relative mx-auto w-full pt-0'>
                <div className='relative'>
                    <CarImage imageUrl={auction.imageUrl} />
                    {/* Rozet resmin uzerinde kalir ama kart genisligini asamaz */}
                    <div className='absolute top-2 left-2 max-w-[calc(100%-1rem)]'>
                        <CurrentHighBidBadge 
                            reservePrice={auction.reservePrice} 
                            amount={auction.currentHighBid}
                            auctionId={auction.id}
                        />
                    </div>
                </div>
                <CardContent className='flex flex-col gap-2'>
                    <div className='flex items-center justify-between gap-2'>
                        <h3 className='min-w-0 truncate text-muted-foreground'>
                            {auction.make} {auction.model}
                        </h3>
                        <p className='shrink-0 text-sm font-semibold'>{auction.year}</p>
                    </div>
                    <div className='flex items-center justify-between gap-2'>
                        <Badge className='min-w-0 max-w-[60%]' variant='secondary'>
                            <User />
                            <span className='min-w-0 truncate'>{auction.seller}</span>
                        </Badge>
                        <CountdownTimer auctionEnd={auction.auctionEnd} />
                    </div>
                </CardContent>
            </Card>
        </Link>
        
    );
}