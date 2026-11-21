# TourFlow Angular frontend

Angular 22 standalone application for the TourFlow public, customer, operations, administration, and driver portals.

## Development

Start the ASP.NET Core API at `https://localhost:7081`, then:

```powershell
npm install
npm start
```

Open `http://localhost:4200`. Change the API URL in `src/environments/environment.ts` for another local endpoint.

## Verification

```powershell
npm run build
npm test -- --watch=false
```

Do not put API credentials, JWT keys, or database connection strings in Angular environment files. Angular configuration is delivered to the browser and is public.
