import AuctionCard from "@/features/listings/AuctionCard";
import {Auction} from "@/lib/types";

type Props = {
    auctions: Auction[];
}

export default async function Listings({auctions}: Props) {

    
    return (
        <div className='grid grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-6 lg:grid-cols-3 xl:grid-cols-4'>
            {auctions.map(auction => (
                <AuctionCard auction={auction} key={auction.id} />
            ))}
        </div>
    );
}