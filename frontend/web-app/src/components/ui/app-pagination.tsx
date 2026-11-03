'use client'

import {
    Pagination,
    PaginationContent,
    PaginationEllipsis,
    PaginationItem,
    PaginationLink,
    PaginationNext,
    PaginationPrevious,
} from "@/components/ui/pagination"
import {usePathname, useRouter, useSearchParams} from "next/navigation";
import {Button} from "@/components/ui/button";

type Props = {
    page: number
    pageSize: number
    totalCount: number
}

const PAGE_SIZE_OPTIONS = [4,8,12];

export function AppPagination({page, pageSize, totalCount}: Props) {
    const router = useRouter()
    const pathname = usePathname();
    const searchParams = useSearchParams();

    const totalPages = Math.ceil(totalCount / pageSize);

    const goToPage = (pageNumber: number) => {
        if (pageNumber < 1 || pageNumber > totalPages || pageNumber === page) return;

        const params = new URLSearchParams(searchParams.toString());
        params.set('pageNumber', pageNumber.toString());
        router.push(`${pathname}?${params.toString()}`);
    }
    
    const setPageSize = (newPageSize: number) => {
        if (newPageSize === pageSize) return;

        const params = new URLSearchParams(searchParams.toString());
        params.set('pageNumber', '1');
        params.set('pageSize', newPageSize.toString())
        router.push(`${pathname}?${params.toString()}`);
    }

    const pageNumbers: (number | 'ellipsis')[] = [];
    for (let i = 1; i <= totalPages; i++) {
        if (i === 1 || i === totalPages || Math.abs(i - page) <= 1) {
            pageNumbers.push(i);
        } else if (pageNumbers[pageNumbers.length - 1] !== 'ellipsis') {
            pageNumbers.push('ellipsis');
        }
    }
    
    const firstResult = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
    const lastResult = Math.min(page * pageSize, totalCount);

    return (
        <Pagination
            className='fixed inset-x-0 bottom-0 z-50 flex w-full max-w-none items-center justify-between gap-2 border-t border-border/70 bg-background/70 px-3 py-2 backdrop-blur-2xl sm:px-6 lg:px-10'>
            <div className='hidden text-sm text-muted-foreground sm:block'>
                {firstResult} - {lastResult} of {totalCount} results
            </div>
            
            <PaginationContent className='max-w-full overflow-x-auto'>
                <PaginationItem>
                    <PaginationPrevious
                        href="#"
                        onClick={e => {
                            e.preventDefault()
                            goToPage(page - 1)
                        }}
                        className={page <= 1 ? 'pointer-events-none opacity-50' : undefined}
                    />
                </PaginationItem>

                {pageNumbers.map((pageNumber, index) => pageNumber === 'ellipsis' ? (
                    <PaginationItem key={index}>
                        <PaginationEllipsis/>
                    </PaginationItem>
                ) : (
                    <PaginationItem key={index}>
                        <PaginationLink
                            href="#"
                            isActive={pageNumber === page}
                            onClick={e => {
                                e.preventDefault()
                                goToPage(pageNumber)
                            }}
                        >
                            {pageNumber}
                        </PaginationLink>
                    </PaginationItem>
                ))}

                <PaginationItem>
                    <PaginationNext
                        href="#"
                        onClick={e => {
                            e.preventDefault()
                            goToPage(page + 1)
                        }}
                        className={page >= totalPages ? 'pointer-events-none opacity-50' : undefined}
                    />
                </PaginationItem>
            </PaginationContent>
            <div className='hidden items-center gap-1 md:flex'>
                <span className='text-sm text-muted-foreground'>Show:</span>
                {PAGE_SIZE_OPTIONS.map(size => (
                    <Button
                        key={size}
                        type='button'
                        size='sm'
                        variant={size === pageSize ? 'outline' : 'ghost'}
                        onClick={() => setPageSize(size)}
                    >
                        {size}
                    </Button>
                ))}
            </div>
        </Pagination>
    )
}
