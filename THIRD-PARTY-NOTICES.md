# Third-party notices

AniSteam is licensed under AGPL-3.0-only. Corresponding source, licenses and scripts to rebuild and replace the complete helper/agent are provided in the separate same-version `AniSteam-<version>-source.zip` companion, alongside the minimal binary patch. The companion is not needed in the Animeko installation directory.

| Component | Version / revision | License / source |
| --- | --- | --- |
| Animeko launcher configuration and integration reference | v6.3.0 | [AGPL-3.0](https://github.com/open-ani/animeko/tree/v6.3.0), see project `LICENSE` |
| yySync Steam status protocol reference | 1820a20e9099b7350d306483cd93083f68d2153f | [MIT](https://github.com/wuyan1337/yySync), `licenses/yySync-MIT.txt` |
| ASM | 9.8 | [BSD-3-Clause](https://asm.ow2.io/), `licenses/ASM-BSD-3-Clause.txt` |
| SteamKit2 | 3.4.0 | [LGPL-2.1-only](https://github.com/SteamRE/SteamKit/tree/3.4.0), `licenses/SteamKit2-LGPL-2.1.txt` and `licenses/SteamKit2-third-party.txt` |
| QRCoder | 1.6.0 | [MIT](https://github.com/codebude/QRCoder/tree/v1.6.0), `licenses/QRCoder-MIT.txt` |
| protobuf-net / protobuf-net.Core | 3.2.56 | [Apache-2.0](https://github.com/protobuf-net/protobuf-net), `licenses/protobuf-net.txt` |
| ZstdSharp.Port | 0.8.7 | [MIT](https://github.com/oleg-st/ZstdSharp/tree/0.8.7), `licenses/ZstdSharp-MIT.txt`; upstream Zstandard license also included |
| .NET / Windows Desktop Runtime | 10.0.13 | [MIT and third-party notices](https://github.com/dotnet), `licenses/dotnet-*` |
| System.IO.Hashing | 10.0.1 | [MIT](https://github.com/dotnet/runtime), .NET licenses above |

Copyright remains with the respective authors. SteamKit2 is used without modification. The provided project source and standard NuGet references allow rebuilding/relinking AniSteam with a modified SteamKit2; replace the helper executable in `app/ani-steam` with the rebuilt version. SteamKit2's corresponding source for 3.4.0 is available from the linked upstream tag.

No Animeko executable, application JAR or Java runtime is redistributed in the patch. Animeko must already be installed. Steam is a trademark of Valve; this project is independent of Valve and OpenAni.
