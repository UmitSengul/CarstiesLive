'use client';

import {createContext, useContext, useEffect, useRef, useState} from "react";
import {HubConnection, HubConnectionBuilder} from "@microsoft/signalr";

const SignalRContext = createContext<HubConnection | null>(null);

export default function SignalRProvider({children}: { children: React.ReactNode }) {
    const [connection] = useState<HubConnection | null>(() => {
        if (typeof window === 'undefined') return null;

        return new HubConnectionBuilder()
            // Ayni kokenden baglanir: edge `carsties.` uzerinde /notifications'i
            // carsties-api'ye proxy'ler. Boylece CORS ve derleme aninda gomulen
            // NEXT_PUBLIC_BASE_API bagimliligi kalkar (2026-11-19).
            .withUrl('/notifications')
            .withAutomaticReconnect()
            .build()
    });
    
    const startedRef = useRef(false);

    useEffect(() => {
        if (!connection || startedRef.current) return;
        startedRef.current = true;
        connection.start().catch(err => 
            console.error('SignalR connection failed', err));
    }, [connection]);

    return (
        <SignalRContext.Provider value={connection}>
            {children}
        </SignalRContext.Provider>
    );
}

export function useSignalR() {
    return useContext(SignalRContext)
}