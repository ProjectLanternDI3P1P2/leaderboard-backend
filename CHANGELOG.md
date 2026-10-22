# Changelog

## [1.2.0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/compare/v1.1.0...v1.2.0) (2026-10-22)


### Added

* consume published contract packages ([490d84f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/490d84f78706dcbcbe5fbafbb17155eb066a80a7))
* consume published contract packages ([09f629d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/09f629d664cd3a60f6186317840a4a3b656f27fc))
* **database:** migrate and seed development data ([106e5c1](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/106e5c10a13fee3efd178a4734db630c471ead8f))
* **grpc:** propagate correlation IDs ([6249dc5](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/6249dc5524b2ce1ba5413736f200f823fcfb7fa6))
* **migrations:** add migration-only command for GitOps jobs ([441be7d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/441be7db11f1ce238158aa161b6a65a473ab62d7))
* **migrations:** add migration-only command for GitOps jobs ([06a3438](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/06a3438067a0c7b5a2b09dcefb67493c93da26ef))
* **openapi:** persist user ID in Scalar ([7a028a6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7a028a64f7db4678b8dde620ffb758b61501385b))
* separate package and local Docker builds ([3c70757](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/3c70757b9b94a148d23ee92377fbb3a9f4384dd1))
* separate package and local Docker builds ([aa95e19](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/aa95e19cabee8c7e9f318d5ca82593718665abe4))
* **template:** complete database and integration examples ([dfbf560](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/dfbf56055d7bb360c6defdd8a301a5ddd194f08d))


### Fixed

* **cd:** promote the leaderboard application ([5392a2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5392a2e59afaf2972249dd0c5b5d6105a3166d0b))
* **cd:** promote the leaderboard application ([d23570b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d23570b66e50c8b37dd1023b76892de3be0dd96d))
* **cd:** publish images through groupe7 Harbor domain ([65ab975](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/65ab975859b9f3b950a094ff1f0a7e6e78d4e2b6))
* **cd:** publish through private Harbor 10.4.111.200:18443 ([15a328a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/15a328adf6a246365a29b0ee3176b8f49e14df0a))
* **cd:** publish through private Harbor endpoint ([72a4bf4](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/72a4bf489fba9a98b4e1d3c798e1299d0be4e9d4))
* **cd:** use shared workflow with new Harbor endpoint ([92323b0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/92323b05ea3bf72fb6baa5283cafaf91b58f57b5))
* **ci:** grant package access to reusable workflows ([8e2a37a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8e2a37a5ed92ea5d311d892b8c4c381364e6e162))
* **ci:** initialize runner NuGet configuration ([8acbc2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8acbc2ee99b184919369de17fe484d061cb63212))
* **ci:** keep NuGet credentials out of Sonar analysis ([2b3c0d3](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/2b3c0d305cb2b65c9ce2c013837d98d937df3b2a))
* **ci:** keep NuGet credentials outside repository ([f02f79b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f02f79bd86befe73e05bdf2819c04dda6f1e442e))
* **ci:** pin reusable workflow revision ([48042c8](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/48042c82edbeaeba5daa7e75730e63c1a032bbfc))
* **ci:** provision PostgreSQL for Sonar tests ([1d00b61](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/1d00b614297738f8366eef71d9652f30a53da04d))
* **ci:** restore package authentication and publication ([632b743](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/632b743b138ef58bd4023b600872e16d90964283))
* **ci:** use runner NuGet configuration ([795b7df](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/795b7df10958f8a811f11ec5318ca70a3e78264d))
* **config:** isolate development service settings ([5093b9b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5093b9bedef6c8dbb18660ee728549addd67afe1))
* **database:** retain migration workflow ([9498065](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/94980659d238d4b6d835c839bedbd1f9601e5035))
* **docker:** include dockerignore in application project ([7712bcb](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7712bcb44e6699fa965cc4fe6eaaefacbe0afd06))
* **integration:** configure leaderboard test database ([f070937](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f07093736f588d202a158a75186433be88d14b63))


### Changed

* **docker:** update Dockerfile for Leaderboard ([f1c2af2](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f1c2af2a174f5302eba2f1658295010c018b164d))
* **service:** rename combat service to leaderboard ([8385a1f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8385a1f6050dfbf5bafec0ea608471042ee651af))
* **service:** rename combat service to leaderboard ([c805e9d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/c805e9d02b18bf442e5ec1752ccc0842d558da95))

## [1.1.0](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/compare/v1.0.0...v1.1.0) (2026-09-23)


### Added

* add ef core migration tooling ([b3abca4](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/b3abca46187ee86de0eda40e2da4ef26ef68e275))
* add ef core migration tooling ([e62b3f1](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/e62b3f12e77dce1622db927db6ad55cceaf05166))
* enforce command transaction boundary ([4096387](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/40963875235dde96cbfbe0d22352baf86f1d5255))
* publish player created events through RabbitMQ ([40727b6](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/40727b6125a422e1cbbeefaab2d17520c4ce2650))
* publish player created events through RabbitMQ ([cccf22e](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/cccf22eebfad251aee42150c4ba83fea8879c20c))

## [1.0.0](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/compare/v0.1.1...v1.0.0) (2026-09-22)


### ⚠ BREAKING CHANGES

* **contracts:** release protobuf contracts independently

### Added

* **contracts:** publish protobuf package ([5df7049](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/5df7049dcb876f7f821bc58759f0fef43d7e6ec6))
* **contracts:** release protobuf contracts independently ([d7cefcb](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/d7cefcb8b517075887b9f33c90c4262bac64a212))
* **grpc:** add infrastructure player client ([00a2531](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/00a2531a16d93773e7bed45485eefc608314030a))
* **grpc:** expose player service internally ([b374c80](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/b374c80622924deaa333dabc33f350175f8b1e88))
* Mise en place d'un exemple de service GRPC client et serveur ([550c759](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/550c759e0f5de9d7d8cebb07b2f489016918cb07))

## [0.1.1](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/compare/v0.1.0...v0.1.1) (2026-09-05)


### Fixed

* run the container as the unprivileged app user ([#8](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/issues/8)) ([894be3f](https://github.com/ProjectLanternDI3P1P2/dotnet-backend-template/commit/894be3fe28e64218726b2dbc2e2bccb7efb9846b))
