# Changelog

## [0.2.2](https://github.com/khavishbhundoo/ChronoQueue/compare/0.2.1...0.2.2) (2025-09-27)


### Build

* Bump the nuget-dependencies group with 1 update ([#41](https://github.com/khavishbhundoo/ChronoQueue/issues/41)) ([3f0ac2f](https://github.com/khavishbhundoo/ChronoQueue/commit/3f0ac2fa28bb4f51d78c383498535c61a4ae1f7f))
* Bump the nuget-dependencies group with 1 update ([#44](https://github.com/khavishbhundoo/ChronoQueue/issues/44)) ([0d127b4](https://github.com/khavishbhundoo/ChronoQueue/commit/0d127b43d9cd0805524173031ad63d14844934db))
* **deps:** Bump actions/checkout from 4 to 5 ([#39](https://github.com/khavishbhundoo/ChronoQueue/issues/39)) ([b9175a3](https://github.com/khavishbhundoo/ChronoQueue/commit/b9175a308071f0bc1f4d010a83c3f2d66319add4))
* **deps:** Bump actions/setup-dotnet from 4 to 5 ([#42](https://github.com/khavishbhundoo/ChronoQueue/issues/42)) ([a6be843](https://github.com/khavishbhundoo/ChronoQueue/commit/a6be84336ad28dfe1fa01052b28b9208918e1d92))


### Performance Improvements

* prevent false sharing with padding for shared variables ([#45](https://github.com/khavishbhundoo/ChronoQueue/issues/45)) ([f590c6a](https://github.com/khavishbhundoo/ChronoQueue/commit/f590c6aa6a2ae7f05b830c579b9611b097d9ed12))

## [0.2.1](https://github.com/khavishbhundoo/ChronoQueue/compare/0.2.0...0.2.1) (2025-07-18)


### Build

* Bump the nuget-dependencies group with 1 update ([#38](https://github.com/khavishbhundoo/ChronoQueue/issues/38)) ([2ba8a6e](https://github.com/khavishbhundoo/ChronoQueue/commit/2ba8a6eba690004f10d125cc6066b57a1791dc8c))


### Refactoring

* extract methods and final flush cleanup ([#36](https://github.com/khavishbhundoo/ChronoQueue/issues/36)) ([73a7079](https://github.com/khavishbhundoo/ChronoQueue/commit/73a7079fe85d32cbc86e1adab731fb1e4d59b7a2))
* minor cleanup ([#34](https://github.com/khavishbhundoo/ChronoQueue/issues/34)) ([52989ad](https://github.com/khavishbhundoo/ChronoQueue/commit/52989ad7474009294c781d919a133cbec89fba6a))


### Performance Improvements

* reduce memory allocation in flush ([#37](https://github.com/khavishbhundoo/ChronoQueue/issues/37)) ([2c497f3](https://github.com/khavishbhundoo/ChronoQueue/commit/2c497f3ec8ca54a3af21a4efd0a23059440a07c6))

## [0.2.0](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.8...0.2.0) (2025-07-16)


### Features

* Add disposeOnFlush ([#33](https://github.com/khavishbhundoo/ChronoQueue/issues/33)) ([f934375](https://github.com/khavishbhundoo/ChronoQueue/commit/f934375ff8fbca7569507d78f80bc5c3a1f30f83))


### Bug fixes

* Dispose expired items on flush and minor cleanup ([#31](https://github.com/khavishbhundoo/ChronoQueue/issues/31)) ([a216ce4](https://github.com/khavishbhundoo/ChronoQueue/commit/a216ce4c51804704eb88777fb3d425c1828d9996))


### Build

* Bump the nuget-dependencies group with 1 update ([#30](https://github.com/khavishbhundoo/ChronoQueue/issues/30)) ([d1eee18](https://github.com/khavishbhundoo/ChronoQueue/commit/d1eee185db888e7397bb5589893fdfbd5800e0b4))
* Bump the nuget-dependencies group with 1 update ([#32](https://github.com/khavishbhundoo/ChronoQueue/issues/32)) ([c2d0edf](https://github.com/khavishbhundoo/ChronoQueue/commit/c2d0edf340afbdf5b84b4e7d6dd64581723dd6f6))


### Documentation updates

* Fix typo in README ([#29](https://github.com/khavishbhundoo/ChronoQueue/issues/29)) ([1408bf3](https://github.com/khavishbhundoo/ChronoQueue/commit/1408bf3245c74d6ec69145daeb5c9519aabf011a))
* update benchmarks ([#27](https://github.com/khavishbhundoo/ChronoQueue/issues/27)) ([596b8f1](https://github.com/khavishbhundoo/ChronoQueue/commit/596b8f1cda487f508f6d1f813e6750c0c1227536))

## [0.1.8](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.7...0.1.8) (2025-06-12)


### Build

* Bump the nuget-dependencies group with 1 update ([#23](https://github.com/khavishbhundoo/ChronoQueue/issues/23)) ([6890904](https://github.com/khavishbhundoo/ChronoQueue/commit/689090447439bb0ed09c8b6bc083f5302a147897))


### Performance Improvements

* Remove MemoryCache ([#26](https://github.com/khavishbhundoo/ChronoQueue/issues/26)) ([ae87536](https://github.com/khavishbhundoo/ChronoQueue/commit/ae875369cfdbe4cf1f784ab10cb69ac163eb61b0))


### Documentation updates

* Export benchmarks as JSON ([#24](https://github.com/khavishbhundoo/ChronoQueue/issues/24)) ([2ffcc3e](https://github.com/khavishbhundoo/ChronoQueue/commit/2ffcc3ea7dd764c4defdf57af8187dfc9b7475d7))

## [0.1.7](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.6...0.1.7) (2025-06-08)


### Performance Improvements

* reduce MemoryCache boxing ([#21](https://github.com/khavishbhundoo/ChronoQueue/issues/21)) ([af83013](https://github.com/khavishbhundoo/ChronoQueue/commit/af83013fc1f31ba3a3e96b5e0cb7a8cfec37e5a3))


### Documentation updates

* clarify ChronoQueue locking behavior ([#19](https://github.com/khavishbhundoo/ChronoQueue/issues/19)) ([ebf2847](https://github.com/khavishbhundoo/ChronoQueue/commit/ebf284758d99be862eb5d669ce3e50c9a3f5908e))

## [0.1.6](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.5...0.1.6) (2025-06-08)


### Performance Improvements

* Improve memory compaction frequency ([#18](https://github.com/khavishbhundoo/ChronoQueue/issues/18)) ([efed5a9](https://github.com/khavishbhundoo/ChronoQueue/commit/efed5a981e66d02a286c85a9ecacdfc160cf20c4))


### Documentation updates

* Improve documentation for API methods ([#16](https://github.com/khavishbhundoo/ChronoQueue/issues/16)) ([07d7d06](https://github.com/khavishbhundoo/ChronoQueue/commit/07d7d06ffc242b915a481c7035e8d0ca77a5cab3))

## [0.1.5](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.4...0.1.5) (2025-06-07)


### Refactoring

* Cleanup properties from csproj & update readme ([#14](https://github.com/khavishbhundoo/ChronoQueue/issues/14)) ([ce5f8d8](https://github.com/khavishbhundoo/ChronoQueue/commit/ce5f8d8a86981b6da4bfa88b5af12c037c9310b4))

## [0.1.4](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.3...0.1.4) (2025-06-07)


### Build

* include symbols during packing ([#12](https://github.com/khavishbhundoo/ChronoQueue/issues/12)) ([0d28a0a](https://github.com/khavishbhundoo/ChronoQueue/commit/0d28a0a23399b069023745204bdfc90b959936bf))

## [0.1.3](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.2...0.1.3) (2025-06-07)


### Build

* fix syntax for pushing nuget ([#10](https://github.com/khavishbhundoo/ChronoQueue/issues/10)) ([ad048f0](https://github.com/khavishbhundoo/ChronoQueue/commit/ad048f03a7256b0ce94f5f10b59e475c7f755ec5))

## [0.1.2](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.1...0.1.2) (2025-06-07)


### Build

* publish symbols package ([#8](https://github.com/khavishbhundoo/ChronoQueue/issues/8)) ([ae814e4](https://github.com/khavishbhundoo/ChronoQueue/commit/ae814e4f5e38d3b21da3c1342aa73117947e26e1))

## [0.1.1](https://github.com/khavishbhundoo/ChronoQueue/compare/0.1.0...0.1.1) (2025-06-07)


### Build

* Add missing metadata for nuget.org publishing ([#7](https://github.com/khavishbhundoo/ChronoQueue/issues/7)) ([cc4b758](https://github.com/khavishbhundoo/ChronoQueue/commit/cc4b75879cdd95a15100078fbe2358a3fc66fd57))
* Do not pack benchmark project ([#5](https://github.com/khavishbhundoo/ChronoQueue/issues/5)) ([dde817c](https://github.com/khavishbhundoo/ChronoQueue/commit/dde817c94f9247fcffac9d82e1066e6c612354ee))

## [0.1.0](https://github.com/khavishbhundoo/ChronoQueue/compare/0.0.1...0.1.0) (2025-06-07)


### Features

* Introduce ChronoQueue ([#1](https://github.com/khavishbhundoo/ChronoQueue/issues/1)) ([191300e](https://github.com/khavishbhundoo/ChronoQueue/commit/191300edbac38e2d7fcb87bf8460ac78a49520e9))


### Build

* Bump the nuget-dependencies group with 4 updates ([#3](https://github.com/khavishbhundoo/ChronoQueue/issues/3)) ([c34da79](https://github.com/khavishbhundoo/ChronoQueue/commit/c34da79f5435f31730e39e10600b49bb2ddf0cd8))
* Use new project key for sonarqube ([#4](https://github.com/khavishbhundoo/ChronoQueue/issues/4)) ([358e1cb](https://github.com/khavishbhundoo/ChronoQueue/commit/358e1cb5301f822ad3d8218a059b2d6684dc203a))
