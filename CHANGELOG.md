# Changelog

## [2.0.0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/compare/v1.2.0...v2.0.0) (2026-11-27)


### ⚠ BREAKING CHANGES

* **contracts:** release protobuf contracts independently

### Added

* add ef core migration tooling ([b3abca4](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b3abca46187ee86de0eda40e2da4ef26ef68e275))
* add ef core migration tooling ([e62b3f1](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/e62b3f12e77dce1622db927db6ad55cceaf05166))
* **api:** expose warehouse rebuilds, player statistics and league table ([a7c4e55](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/a7c4e554fcfb110539f3cbde84af9820b665196e))
* **application:** rebuild the warehouse from the raw event store ([d18af29](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d18af29788d1e130a21ef998265fde1493392958))
* **collection:** archive les événements bruts ([5a77dff](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5a77dff7df28bc1538465ddfd7bcee1c0867be27))
* consume published contract packages ([490d84f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/490d84f78706dcbcbe5fbafbb17155eb066a80a7))
* consume published contract packages ([09f629d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/09f629d664cd3a60f6186317840a4a3b656f27fc))
* **contracts:** partage le package NuGet ([8f65b7e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8f65b7ed6cb70a46b5270175e5e24322924aa823))
* **contracts:** publish protobuf package ([5df7049](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5df7049dcb876f7f821bc58759f0fef43d7e6ec6))
* **contracts:** release protobuf contracts independently ([d7cefcb](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d7cefcb8b517075887b9f33c90c4262bac64a212))
* **database:** migrate and seed development data ([106e5c1](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/106e5c10a13fee3efd178a4734db630c471ead8f))
* **domain:** model warehouse generations and rebuilds ([73d3579](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/73d3579ff46e741af0f10961aed7bb30c52b9453))
* enforce command transaction boundary ([4096387](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/40963875235dde96cbfbe0d22352baf86f1d5255))
* enforce command transaction boundary ([8bbedf6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8bbedf6877115ad15cc7455e50cf2946f1c05750))
* **events:** add Mongo raw event store ([98ff8ea](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/98ff8ea9a0451f392df2b5a7f3b39bf0569948f6))
* **events:** create the raw event collection at development startup ([2f8fddd](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/2f8fdddf4cba1d80643ff022b5bc0b6534e48484))
* **events:** define RawEvent model and IRawEventStore ([2a1f7fc](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/2a1f7fca109003e59f01e7a341084a4176e28321))
* **events:** fusionne contrats et stockage ([de8255d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/de8255d3c031407fa8edf57466c00d9c71c50e09))
* **events:** intégrer contrats, archivage et warehouse (reprise de [#19](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/issues/19)) ([4384c92](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/4384c922dbc0e3c33994ea130d068e36053671f5))
* **events:** read mongo connection string from ConnectionStrings ([e7777f3](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/e7777f3d5cb8887c6364ed890d248bf0faf950bb))
* **grpc:** add infrastructure player client ([00a2531](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/00a2531a16d93773e7bed45485eefc608314030a))
* **grpc:** expose player service internally ([b374c80](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b374c80622924deaa333dabc33f350175f8b1e88))
* **grpc:** propagate correlation IDs ([6249dc5](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/6249dc5524b2ce1ba5413736f200f823fcfb7fa6))
* **messaging:** persist received events before ack ([16d53b5](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/16d53b5f4599686f3591de4c67b7a2d1d677c678))
* **migrations:** add migration-only command for GitOps jobs ([441be7d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/441be7db11f1ce238158aa161b6a65a473ab62d7))
* **migrations:** add migration-only command for GitOps jobs ([06a3438](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/06a3438067a0c7b5a2b09dcefb67493c93da26ef))
* Mise en place d'un exemple de service GRPC client et serveur ([550c759](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/550c759e0f5de9d7d8cebb07b2f489016918cb07))
* **openapi:** persist user ID in Scalar ([7a028a6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7a028a64f7db4678b8dde620ffb758b61501385b))
* **persistence:** store and rebuild warehouse generations in PostgreSQL ([a874bd8](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/a874bd8b9c2331520e7ce44ec5620e2bf1900036))
* publish player created events through RabbitMQ ([40727b6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/40727b6125a422e1cbbeefaab2d17520c4ce2650))
* publish player created events through RabbitMQ ([cccf22e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/cccf22eebfad251aee42150c4ba83fea8879c20c))
* route leaderboard API by service prefix ([77ee995](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/77ee995a0fcbd3b31f825fe7c88b78d10a297782))
* separate package and local Docker builds ([3c70757](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/3c70757b9b94a148d23ee92377fbb3a9f4384dd1))
* separate package and local Docker builds ([aa95e19](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/aa95e19cabee8c7e9f318d5ca82593718665abe4))
* **template:** complete database and integration examples ([dfbf560](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/dfbf56055d7bb360c6defdd8a301a5ddd194f08d))


### Fixed

* address the Sonar findings in the presentation and domain layers ([#13](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/issues/13)) ([9e6d9bf](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/9e6d9bfb105c393a2ae62989da2f98ba52cf7774))
* align leaderboard ownership and routing with Player ([c3eacd9](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/c3eacd9097626382f15b392cacbf404ce085f31c))
* **cd:** promote the leaderboard application ([5392a2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5392a2e59afaf2972249dd0c5b5d6105a3166d0b))
* **cd:** promote the leaderboard application ([d23570b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d23570b66e50c8b37dd1023b76892de3be0dd96d))
* **cd:** publish images through groupe7 Harbor domain ([65ab975](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/65ab975859b9f3b950a094ff1f0a7e6e78d4e2b6))
* **cd:** publish through private Harbor 10.4.111.200:18443 ([15a328a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/15a328adf6a246365a29b0ee3176b8f49e14df0a))
* **cd:** publish through private Harbor endpoint ([72a4bf4](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/72a4bf489fba9a98b4e1d3c798e1299d0be4e9d4))
* **cd:** use shared workflow with new Harbor endpoint ([92323b0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/92323b05ea3bf72fb6baa5283cafaf91b58f57b5))
* **ci:** account for warehouse state and require broker credentials ([6952e67](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/6952e670330cbf43cd8f8df18d3ab02642ae252e))
* **ci:** grant package access to reusable workflows ([8e2a37a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8e2a37a5ed92ea5d311d892b8c4c381364e6e162))
* **ci:** initialize runner NuGet configuration ([8acbc2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8acbc2ee99b184919369de17fe484d061cb63212))
* **ci:** keep Mongo test credentials out of the connection string ([eca5e1a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/eca5e1a7b1aa7107756253c7eee049d675817041))
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
* make the Docker image build, and add a compose stack to run it ([#14](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/issues/14)) ([aa20e81](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/aa20e81a4f3f695e4dd66f2a9e648bf4e29869b3))
* **messaging:** bind only the progression exchange outside local compose ([b72679a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b72679a07eb63fefbbd3b577cdd438910f7c4fd1))
* **messaging:** bind the raw event queue to every producer exchange ([5ece8f6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5ece8f6d9954e496e26e1112dead07398d6122b1))
* **messaging:** register the raw event batch processor ([34aed8b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/34aed8b22e794bb1c15cd6b4b12ce7b12b232330))
* probe readiness without a shell in local containers ([674a278](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/674a278c7e1ff92ac55cfedb7fd2b3e94a88d29c))
* remove duplicate player ownership ([7c3bba0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7c3bba04ae668e93977e75a521de63545106e818))
* require trusted identity and realm role for rebuilds ([7e1090e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7e1090e30d3e927f75422d3546bbd20173b6f125))
* run the container as the unprivileged app user ([#8](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/issues/8)) ([894be3f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/894be3fe28e64218726b2dbc2e2bccb7efb9846b))


### Changed

* **docker:** update Dockerfile for Leaderboard ([f1c2af2](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f1c2af2a174f5302eba2f1658295010c018b164d))
* **events:** name repeated mongo schema literals ([f261235](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f26123592fa97f55ec7a8783a3db430ce5e1a232))
* isolate and test the readiness probe ([947a249](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/947a24936d23985c65b764366fd15b7ff284463b))
* **service:** rename combat service to leaderboard ([8385a1f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8385a1f6050dfbf5bafec0ea608471042ee651af))
* **service:** rename combat service to leaderboard ([c805e9d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/c805e9d02b18bf442e5ec1752ccc0842d558da95))
* use command marker for transaction boundary ([b693cee](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b693cee1f1e1fad8d32890b5e61da572174ad6ff))

## [1.2.0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/compare/v1.1.0...v1.2.0) (2026-11-24)


### Added

* **api:** expose warehouse rebuilds, player statistics and league table ([a7c4e55](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/a7c4e554fcfb110539f3cbde84af9820b665196e))
* **application:** rebuild the warehouse from the raw event store ([d18af29](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d18af29788d1e130a21ef998265fde1493392958))
* **collection:** archive les événements bruts ([5a77dff](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5a77dff7df28bc1538465ddfd7bcee1c0867be27))
* consume published contract packages ([490d84f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/490d84f78706dcbcbe5fbafbb17155eb066a80a7))
* consume published contract packages ([09f629d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/09f629d664cd3a60f6186317840a4a3b656f27fc))
* **contracts:** partage le package NuGet ([8f65b7e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8f65b7ed6cb70a46b5270175e5e24322924aa823))
* **database:** migrate and seed development data ([106e5c1](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/106e5c10a13fee3efd178a4734db630c471ead8f))
* **domain:** model warehouse generations and rebuilds ([73d3579](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/73d3579ff46e741af0f10961aed7bb30c52b9453))
* **events:** add Mongo raw event store ([98ff8ea](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/98ff8ea9a0451f392df2b5a7f3b39bf0569948f6))
* **events:** create the raw event collection at development startup ([2f8fddd](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/2f8fdddf4cba1d80643ff022b5bc0b6534e48484))
* **events:** define RawEvent model and IRawEventStore ([2a1f7fc](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/2a1f7fca109003e59f01e7a341084a4176e28321))
* **events:** fusionne contrats et stockage ([de8255d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/de8255d3c031407fa8edf57466c00d9c71c50e09))
* **events:** intégrer contrats, archivage et warehouse (reprise de [#19](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/issues/19)) ([4384c92](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/4384c922dbc0e3c33994ea130d068e36053671f5))
* **events:** read mongo connection string from ConnectionStrings ([e7777f3](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/e7777f3d5cb8887c6364ed890d248bf0faf950bb))
* **grpc:** propagate correlation IDs ([6249dc5](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/6249dc5524b2ce1ba5413736f200f823fcfb7fa6))
* **messaging:** persist received events before ack ([16d53b5](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/16d53b5f4599686f3591de4c67b7a2d1d677c678))
* **migrations:** add migration-only command for GitOps jobs ([441be7d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/441be7db11f1ce238158aa161b6a65a473ab62d7))
* **migrations:** add migration-only command for GitOps jobs ([06a3438](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/06a3438067a0c7b5a2b09dcefb67493c93da26ef))
* **openapi:** persist user ID in Scalar ([7a028a6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7a028a64f7db4678b8dde620ffb758b61501385b))
* **persistence:** store and rebuild warehouse generations in PostgreSQL ([a874bd8](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/a874bd8b9c2331520e7ce44ec5620e2bf1900036))
* route leaderboard API by service prefix ([77ee995](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/77ee995a0fcbd3b31f825fe7c88b78d10a297782))
* separate package and local Docker builds ([3c70757](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/3c70757b9b94a148d23ee92377fbb3a9f4384dd1))
* separate package and local Docker builds ([aa95e19](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/aa95e19cabee8c7e9f318d5ca82593718665abe4))
* **template:** complete database and integration examples ([dfbf560](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/dfbf56055d7bb360c6defdd8a301a5ddd194f08d))


### Fixed

* align leaderboard ownership and routing with Player ([c3eacd9](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/c3eacd9097626382f15b392cacbf404ce085f31c))
* **cd:** promote the leaderboard application ([5392a2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5392a2e59afaf2972249dd0c5b5d6105a3166d0b))
* **cd:** promote the leaderboard application ([d23570b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/d23570b66e50c8b37dd1023b76892de3be0dd96d))
* **cd:** publish images through groupe7 Harbor domain ([65ab975](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/65ab975859b9f3b950a094ff1f0a7e6e78d4e2b6))
* **cd:** publish through private Harbor 10.4.111.200:18443 ([15a328a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/15a328adf6a246365a29b0ee3176b8f49e14df0a))
* **cd:** publish through private Harbor endpoint ([72a4bf4](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/72a4bf489fba9a98b4e1d3c798e1299d0be4e9d4))
* **cd:** use shared workflow with new Harbor endpoint ([92323b0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/92323b05ea3bf72fb6baa5283cafaf91b58f57b5))
* **ci:** account for warehouse state and require broker credentials ([6952e67](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/6952e670330cbf43cd8f8df18d3ab02642ae252e))
* **ci:** grant package access to reusable workflows ([8e2a37a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8e2a37a5ed92ea5d311d892b8c4c381364e6e162))
* **ci:** initialize runner NuGet configuration ([8acbc2e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8acbc2ee99b184919369de17fe484d061cb63212))
* **ci:** keep Mongo test credentials out of the connection string ([eca5e1a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/eca5e1a7b1aa7107756253c7eee049d675817041))
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
* **messaging:** bind only the progression exchange outside local compose ([b72679a](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b72679a07eb63fefbbd3b577cdd438910f7c4fd1))
* **messaging:** bind the raw event queue to every producer exchange ([5ece8f6](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/5ece8f6d9954e496e26e1112dead07398d6122b1))
* **messaging:** register the raw event batch processor ([34aed8b](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/34aed8b22e794bb1c15cd6b4b12ce7b12b232330))
* probe readiness without a shell in local containers ([674a278](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/674a278c7e1ff92ac55cfedb7fd2b3e94a88d29c))
* remove duplicate player ownership ([7c3bba0](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7c3bba04ae668e93977e75a521de63545106e818))
* require trusted identity and realm role for rebuilds ([7e1090e](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/7e1090e30d3e927f75422d3546bbd20173b6f125))


### Changed

* **docker:** update Dockerfile for Leaderboard ([f1c2af2](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f1c2af2a174f5302eba2f1658295010c018b164d))
* **events:** name repeated mongo schema literals ([f261235](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/f26123592fa97f55ec7a8783a3db430ce5e1a232))
* isolate and test the readiness probe ([947a249](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/947a24936d23985c65b764366fd15b7ff284463b))
* **service:** rename combat service to leaderboard ([8385a1f](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/8385a1f6050dfbf5bafec0ea608471042ee651af))
* **service:** rename combat service to leaderboard ([c805e9d](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/c805e9d02b18bf442e5ec1752ccc0842d558da95))
* use command marker for transaction boundary ([b693cee](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/commit/b693cee1f1e1fad8d32890b5e61da572174ad6ff))

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
