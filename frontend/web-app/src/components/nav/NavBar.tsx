import {CarFront} from "lucide-react";
import Link from "next/link";
import SearchInput from "@/components/nav/SearchInput";
import LoginButton from "@/components/nav/LoginButton";
import {getCurrentUser} from "@/lib/auth";
import {UserMenu} from "@/components/nav/UserMenu";
import {buttonVariants} from "@/components/ui/button";
import {Suspense} from "react";

export default async function NavBar() {
    const user = await getCurrentUser();

    return (
        <header className='sticky top-0 z-50 flex flex-wrap items-center justify-between gap-2 bg-background p-3 shadow-lg'>
            <Link href='/' className='flex items-center gap-2 text-xl font-semibold text-red-500 sm:text-3xl'>
                <CarFront className='h-8 w-8 shrink-0 sm:h-[50px] sm:w-[50px]'/>
                <div className='hidden md:block'>Carsties Auctions</div>
            </Link>
            <Suspense>
                <SearchInput/>
            </Suspense>
            {user ? (
                <UserMenu user={user}/>
            ) : (
                <div className='flex items-center gap-2'>
                    <LoginButton/>
                    <Link 
                        className={buttonVariants({variant: 'default', size: 'lg'})}
                        href={`${process.env.NEXT_PUBLIC_ID_URL}/Account/Register`}
                    >
                        Register
                    </Link>
                </div>
                
            )}
        </header>
    )
}