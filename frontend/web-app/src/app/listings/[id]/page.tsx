import {getBidsForListing, getListingDetails} from "@/features/listings/actions";
import NotFound from "next/dist/client/components/builtin/not-found";
import CountdownTimer from "@/features/listings/CountdownTimer";
import CarImage from "@/features/listings/CarImage";
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from "@/components/ui/card";
import MetaCard from "@/features/listings/details/MetaCard";
import {getCurrentUser} from "@/lib/auth";
import Link from "next/link";
import {buttonVariants} from "@/components/ui/button";
import DeleteButton from "@/features/listings/details/DeleteButton";
import BidPanel from "@/features/listings/details/BidPanel";
import AuctionStatusProvider from "@/contexts/AuctionStatusContext";

export default async function ListingDetailedPage(props: PageProps<"/listings/[id]">) {
    const user = await getCurrentUser();
    const {id} = await props.params;
    const result = await getListingDetails(id);
    const bidResult = await getBidsForListing(id);

    if (!result.ok && result.status === 404) return NotFound();
    if (!result.ok) throw new Error(result.error);
    if (!bidResult.ok) throw new Error(bidResult.error);
    
    const {data:auction} = result;
    const {data:bids} = bidResult;

    return (
        <AuctionStatusProvider>
            <div className='flex flex-col'>
                <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
                    <div className='flex flex-wrap items-center gap-3'>
                        <h3 className='text-xl font-semibold sm:text-2xl'>
                            {auction.make} {auction.model}
                        </h3>
                        {user?.username === auction.seller && (
                            <>
                                <Link
                                    href={`/listings/${auction.id}/edit`}
                                    className={buttonVariants({variant: 'outline'})}
                                >
                                    Edit listing
                                </Link>
                                <DeleteButton auction={auction} />
                            </>

                        )}
                    </div>

                    <div className='flex items-center gap-3'>
                    <span className='text-sm uppercase text-muted-foreground sm:text-base'>
                        Time remaining
                    </span>
                        <CountdownTimer auctionEnd={auction.auctionEnd}/>
                    </div>
                </div>
                <div className='mt-3 flex flex-col gap-6 lg:flex-row'>
                    <div className='flex w-full flex-col lg:w-1/2'>
                        <CarImage imageUrl={auction.imageUrl} thumbnail={false}/>
                        <div className='mt-3 flex flex-col gap-3'>
                            <div className='flex flex-wrap gap-3'>
                                <MetaCard label='Mileage' value={auction.mileage}/>
                                <MetaCard label='Color' value={auction.color}/>
                                <MetaCard label='Year' value={auction.year}/>
                                <MetaCard label='Seller' value={auction.seller}/>
                            </div>
                            <MetaCard label='Description' value={auction.description}/>
                        </div>
                    </div>
                    <div className='flex w-full flex-col lg:w-1/2'>
                        <BidPanel bids={bids} auction={auction} />
                    </div>

                </div>
            </div>
        </AuctionStatusProvider>
        
    );
}