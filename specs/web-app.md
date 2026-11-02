# web-app (frontend)

## Summary

Next.js 16 (App Router, React 19) client for the auction site. Talks to the backend exclusively
through GatewayService, using better-auth as a thin session/OAuth layer in front of IdentityService
(generic OIDC provider, authorization code + PKCE) to obtain access tokens for API calls. Gets
real-time updates (new auction, bid placed, auction finished) over a direct SignalR connection to
NotificationService (via the gateway), rendered as toasts.

Note: this is a Next.js app, not a .NET service — the spec format below is adapted rather than
following the backend template verbatim (no NuGet packages / entities / RabbitMQ events).

## Infrastructure

Next.js 16 (App Router), React 19, TypeScript
better-auth (session + generic OAuth2/OIDC client against IdentityService)
@microsoft/signalr (real-time notifications)
Tailwind CSS v4 + shadcn/base-ui components
react-hook-form (forms)

## Key npm packages

better-auth, @microsoft/signalr, @base-ui/react, react-hook-form, date-fns, motion, lucide-react, tailwind-merge, shadcn

## Auth flow

- `src/lib/auth.ts` configures `betterAuth` with a `genericOAuth` plugin pointed at IdentityService's `/connect/authorize`, `/connect/token`, `/connect/userinfo` (client id `nextApp`, PKCE, scopes `openid profile auctionApp`). better-auth 1.7: no `issuer` field (explicit endpoints), sign-in runs through the standard `signIn.social({ provider: "duende" })` flow
- `src/app/api/auth/[...all]/route.ts` — better-auth's catch-all route handler. Next.js strips `basePath` from `req.url` before invoking the handler, so the request path is re-prefixed with `NEXT_PUBLIC_BASE_PATH` (`/carsties/api/auth/...`) to match better-auth's `basePath`
- `src/proxy.ts` — middleware that gates `/session` behind a valid session cookie, redirecting to `/auth/sign-in` otherwise (paths built with the `basePath` prefix)
- `src/lib/fetch-wrapper.ts` — server-side fetch helper that attaches a bearer token to every API call: `auth.api.listUserAccounts()` → find the `duende` account → `auth.api.getAccessToken({ accountId })` (better-auth 1.7 signature), then normalizes responses into `{ ok, data }` / `{ ok: false, status, error }`

## Deployment (path-based single domain)

Production serves the app under `https://umitsengul.online/carsties` instead of a dedicated subdomain:

- `next.config.ts` sets `basePath` from `NEXT_PUBLIC_BASE_PATH` (empty locally → `http://localhost:3000/`, `/carsties` in prod)
- better-auth uses `baseURL=BETTER_AUTH_URL` (origin only, e.g. `https://umitsengul.online`) + `basePath=${NEXT_PUBLIC_BASE_PATH}/api/auth`; the OAuth callback therefore becomes `/carsties/api/auth/callback/duende`
- `/carsties/api/auth/*` must be routed to the Next.js app, not to the gateway (`deploy/nginx/conf.d/default.conf` places that `location` above `/carsties/api/`)
- `NEXT_PUBLIC_*` values are baked at build time (`frontend/web-app/Dockerfile` args, filled by CI)

## Pages / Routes

|Route|Purpose|
|---|---|
|`/`|Home — auction listing grid with search/filter|
|`/listings/create`|Create-auction form|
|`/listings/[id]`|Auction detail — item info, bid history, bid panel, countdown|
|`/listings/[id]/edit`|Edit-auction form (owner only, enforced server-side by AuctionService)|
|`/auth/sign-in`|Sign-in page|
|`/session`|Authenticated-only session/debug page|
|`/api/auth/[...all]`|better-auth route handler (not a user-facing page)|

## API calls consumed (via GatewayService)

All calls go through `fetchWrapper`, base URL = `BASE_API_URL` (GatewayService).

|Function|Method|Gateway route|Auth|
|---|---|---|---|
|`getListings`|GET|`/search?...`|Anon|
|`getListingDetails`|GET|`/auctions/:id`|Anon|
|`createListing`|POST|`/auctions`|Auth|
|`updateListing`|PUT|`/auctions/:id`|Auth|
|`deleteListing`|DELETE|`/auctions/:id`|Auth|
|`getBidsForListing`|GET|`/bids/:id`|Anon|
|`placeBidForAuction`|POST|`/bids?auctionId=&amount=`|Auth|
|`getAuthTest`|POST|`/auctions/test`|Auth|

`placeBidForAuction` calls `revalidatePath` on the listing detail page after a successful bid.

## Real-time events consumed (SignalR)

`src/contexts/SignalRContext.tsx` opens one connection per browser session to `${NEXT_PUBLIC_BASE_API}/notifications` (NotificationService, via the gateway) with automatic reconnect.

`src/contexts/LiveNotifications.tsx` subscribes to:

|Event|Payload|Handling|
|---|---|---|
|`AuctionCreated`|`Auction`|Toast: "New auction created" with year/make/model|
|`AuctionFinished`|`{ itemSold, auctionId, winner?, seller, amount? }`|Re-fetches the listing, toasts either "Sold to X for $Y" or "Reserve not met"|

`BidPlaced` events are handled locally within the listing-detail bid panel/history components rather than as a global toast.

## Key Types

**`Auction`** (`src/lib/types.ts`) — mirrors AuctionService's `AuctionDto`: id, reservePrice, seller, winner?, soldAmount, currentHighBid, createdAt, updatedAt, auctionEnd, status, make, model, description, year, color, mileage, imageUrl

**`Bid`** (`src/lib/types.ts`) — mirrors BidService's `BidDto`: id, auctionId, bidder, bidTime, amount, bidStatus

**`PagedResult<T>`** — `{ results: T[], pageCount: number, totalCount: number }`, matches SearchService's search response shape

**`SessionUser`** (`src/lib/auth.ts`) — inferred from better-auth's session type, extended with a required `username` field
