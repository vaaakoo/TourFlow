# TourFlow contributor instructions

- Preserve Clean Architecture dependencies: Domain is independent; Application depends only on Domain; Infrastructure implements Application and Domain concerns; API is the composition root.
- Implement one approved milestone at a time and update `PROJECT_STATUS.md` and `README.md` after each milestone.
- Use UTC timestamps and `decimal` for money. Do not expose EF entities from API endpoints.
- Keep controllers thin; validation and business rules belong outside controllers.
- Never commit secrets. Use development configuration and environment variables for credentials.
- Run the solution build and relevant tests before handing off a milestone.
