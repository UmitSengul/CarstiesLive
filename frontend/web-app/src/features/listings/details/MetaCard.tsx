type Props = {
    label: string;
    value: string | number;
}

export default function MetaCard({label, value}: Props) {
    return (
        <div className='flex min-w-[7rem] flex-1 flex-col rounded-xl border border-foreground/40 bg-background/75 p-3 sm:p-4'>
            <span className='text-xs uppercase text-muted-foreground'>{label}</span>
            <p className='mt-2 text-sm font-medium'>{value}</p>
        </div>
    );
}