'use client';

import {usePathname, useRouter, useSearchParams} from "next/navigation";
import {InputGroup, InputGroupAddon, InputGroupInput} from "@/components/ui/input-group";
import {Search} from "lucide-react";
import {ChangeEvent, useState} from "react";
import {debounce} from "@/lib/utils";

export default function SearchInput() {
    const router = useRouter();
    const pathname = usePathname();
    const searchParams = useSearchParams();
    const urlSearchTerm = searchParams.get("searchTerm") ?? "";
    const [value, setValue] = useState(() => searchParams.get("searchTerm") ?? "");
    const [prevSearchTerm, setPrevSearchTerm] = useState(urlSearchTerm);
    
    if (urlSearchTerm !== prevSearchTerm) {
        setPrevSearchTerm(urlSearchTerm);
        setValue(urlSearchTerm);
    }
    
    const search = (term: string) => {
        const params = new URLSearchParams(searchParams);
        
        if (term) {
            params.set("searchTerm", term);
        } else {
            params.delete("searchTerm");
        }
        
        const dest = pathname === '/' ? pathname : '/';
        router.push(`${dest}?${params.toString()}`);
    }
    
    const debouncedSearch = debounce(search, 500);
    
    const onChange = (e: ChangeEvent<HTMLInputElement>) => {
        const term = e.target.value;
        setValue(term);
        if (term === ""){
            debouncedSearch.cancel()
            search('')
        } else {
            debouncedSearch(term);
        }
    }
    
    return (
        <InputGroup className='flex w-full items-center rounded-full bg-background py-5 shadow-sm sm:w-1/2 sm:max-w-md'>
            <InputGroupInput 
                type='search'
                placeholder='Search...'
                onChange={onChange}
                value={value}
            />
            <InputGroupAddon>
                <Search />
            </InputGroupAddon>
        </InputGroup>
    );
}