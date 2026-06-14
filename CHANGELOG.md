# Changelog

## [0.1.0](https://github.com/st0o0/puhu.btop/compare/v0.1.0...v0.1.0) (2026-06-14)


* code cleanup ([80c0154](https://github.com/st0o0/puhu.btop/commit/80c0154de27955a875739527fc1e0cee983f153b))


### Features

* add btop page and view model ([4530843](https://github.com/st0o0/puhu.btop/commit/453084327ac218feea1e48729dcfca5c07bfaef3))
* add local plugin testing script ([412b95e](https://github.com/st0o0/puhu.btop/commit/412b95e8353d59cc66977e8e8676a6348a8491a9))
* btop first release ([#1](https://github.com/st0o0/puhu.btop/issues/1)) ([e61a527](https://github.com/st0o0/puhu.btop/commit/e61a527d55f383aabcf42b93a7ac91b5e58dd6c8))
* **build:** Establish project configuration and tooling ([0101b23](https://github.com/st0o0/puhu.btop/commit/0101b2347f73002d4f27b0a6897f976b61b51433))
* live theme re-render in btop page ([7d58296](https://github.com/st0o0/puhu.btop/commit/7d5829687ffb340f1a628db72875b778865cdf8d))
* Migrate tests to Microsoft Testing Platform ([87a14ea](https://github.com/st0o0/puhu.btop/commit/87a14eafdf70f5e8e81e22a312def626757f48af))
* **nodes:** add BtopBoxNode with btop-style title and hotkey badge ([42cef6e](https://github.com/st0o0/puhu.btop/commit/42cef6e021e5b9cce3c5bcce837fa2f33602c4c3))
* **nodes:** add BtopGradients helper for meter colors and glyphs ([f1626b5](https://github.com/st0o0/puhu.btop/commit/f1626b5523fff2ca776a44a26bcee12ae9f59cde))
* **nodes:** add CoreMeterNode gradient per-core meters ([8b68630](https://github.com/st0o0/puhu.btop/commit/8b6863018c437fadd27e3f0499d7ba45edb82888))
* **page:** add RAM block meter; fix BtopBoxNode title truncation without hotkey ([e6b4509](https://github.com/st0o0/puhu.btop/commit/e6b45092adc6bef6c53c80b21f081b2d225de4e7))
* **page:** render boxes with BtopBoxNode title and hotkey badges ([3f96f0b](https://github.com/st0o0/puhu.btop/commit/3f96f0b6e876332cb3da92ac5fd1d2e6d4eefb37))
* **page:** wire CoreMeterNode, disk meters and CPU box clock ([400bd9a](https://github.com/st0o0/puhu.btop/commit/400bd9ad53b8b8d6391e648dd2bdd590c98ceab1))
* port btop layout nodes and metric history test ([407ba59](https://github.com/st0o0/puhu.btop/commit/407ba59a0a1f6759bdbffccb6e1363007447e85d))
* port core models, messages, platform contracts and calculators ([e4ac751](https://github.com/st0o0/puhu.btop/commit/e4ac751123a118326f20b83ca8e9b0bef5b54264))
* port MetricStore and IMetricSink ([eca782d](https://github.com/st0o0/puhu.btop/commit/eca782d21f4e95fe5b4170619f6e59c81b096093))
* port monitoring actors and demand service ([be1f937](https://github.com/st0o0/puhu.btop/commit/be1f9378ecb0e2dfd2af4870939e179ccb224892))
* port platform metric providers with dependency-free windows implementations ([f1e17b6](https://github.com/st0o0/puhu.btop/commit/f1e17b61203a715b9635d7a85625701eb0e962f4))
* Refactor BtopBoxNode and BtopPage for clarity ([88ad6e7](https://github.com/st0o0/puhu.btop/commit/88ad6e79b14f964412b4e53371a5c045a6a5e1cb))
* Refactor BtopBoxNode and BtopPage for clarity ([#4](https://github.com/st0o0/puhu.btop/issues/4)) ([5e08afa](https://github.com/st0o0/puhu.btop/commit/5e08afa691f58ffdfe6ada683fea1ec04847b0f3))
* replace template skeleton with Puhu.Btop project ([fd53a43](https://github.com/st0o0/puhu.btop/commit/fd53a43101f390252d43cbd7dcba5256361e7db9))
* update manifest, readme and workflows for puhu.btop ([55d2f31](https://github.com/st0o0/puhu.btop/commit/55d2f319abd929b46e622ec859122d417f629977))
* wire btop plugin services, actors and route ([abff317](https://github.com/st0o0/puhu.btop/commit/abff31779d0f0a414c9a722769b5af32f594b6b8))
* wire process tree, sort and terminate/kill actions in btop page ([022b1f4](https://github.com/st0o0/puhu.btop/commit/022b1f47e61bcbc3ca2485e1e55a8e887622c5a0))


### Bug Fixes

* ci ([7aa472c](https://github.com/st0o0/puhu.btop/commit/7aa472c4611546b3d61c0608997cac8e889fdb64))
* harden windows disk metrics interop against races and pdh status ([436ae65](https://github.com/st0o0/puhu.btop/commit/436ae65545ebc541ed50fa61a04fbe1a788b05d0))
* negative pid ([3625c26](https://github.com/st0o0/puhu.btop/commit/3625c26c04d92bd6af673c2c57e422e60360979b))
* **page:** make CPU strip meter update reactively ([f2438f9](https://github.com/st0o0/puhu.btop/commit/f2438f97ec9a5f8a5118fcc453829d0c15c9c655))
* **page:** make tree, sort, GPU toggle, filter, and process-action feedback work ([107e05c](https://github.com/st0o0/puhu.btop/commit/107e05c8541fbe19e2e578387c40e8510d4ab231))


### Refactoring

* align repo structure with puhu layout ([a539337](https://github.com/st0o0/puhu.btop/commit/a5393374b59b7d541bc5af795331480ee73ca14b))
* apply Allman braces across nodes, platform metrics, and actors ([c17b2b0](https://github.com/st0o0/puhu.btop/commit/c17b2b06207ba55727dc303b58d152fad5b45497))
* drop docker-only NetworkInfo model from core port ([fea47c7](https://github.com/st0o0/puhu.btop/commit/fea47c752d4964741fbc2a49a43f15bcc51e8d63))
* **nodes:** restore CoreMeterNode item width; make fill test layout-independent ([3f999f2](https://github.com/st0o0/puhu.btop/commit/3f999f28fdf29d993ae73afb28641b9280b15af9))
* resolve monitor actors via DI DependencyResolver ([b74e6d9](https://github.com/st0o0/puhu.btop/commit/b74e6d95114ab202174d6016a32e1b235879e81b))
