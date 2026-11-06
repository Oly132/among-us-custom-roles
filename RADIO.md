# Lobby radio receiver (preview)

This branch adds only the receiving side of the lobby radio. It is not an automatic-update release. Existing installers remain unchanged until a live lobby test passes.

A radio appears in the centre of the waiting lobby while the authorised private connector is connected. Everyone with the receiver, including its owner, hears the audio through Among Us. Audio gets quieter with distance, is panned left/right, and stops outside the lobby. The prop has a solid collision box. Nothing is added to meeting rooms or maps.

The native settings menu has a Radio volume slider below the existing audio sliders. This is a local, persistent preference and is independent of the game's music and effects levels. Custom roles must be enabled.

## Transport and timing

Audio travels through the existing Among Us connection and Impostor server. It uses unreliable 40 ms packets, so dropped music packets cannot hold up reliable gameplay traffic. Blocks are independently decodable 24 kHz mono IMA ADPCM. Signed packets are 585 bytes, about 15 KB/s from the sender before UDP overhead, then relayed per recipient. This is radio-quality audio, not lossless stereo.

A publisher-clock exchange and an 850 ms playback buffer aim to keep receivers on the same audio timeline. High ping, asymmetric routes, packet loss, client stalls and audio-device delays can still cause skew or dropouts. Zero latency regardless of ping is not possible. Late blocks are dropped instead of accumulating delay; consumed slots are cleared to prevent stale audio replay.

Receivers verify P-256 signatures with a pinned public key and bind packets to the current room and sender. The public repository contains no capture program, publishing plugin, private key, private server address or credentials. Installing this receiver does not grant broadcasting permission. Capture, per-app output routing and crash recovery remain private to the owner.

## Validation

The receiver builds against the supported Windows and Android interop assemblies; the Windows binary is also used under Proton on Steam Deck. Protocol/codec tests and a Windows Spotify routing/restoration check passed. Real in-game playback, radio collision, controller/touch settings layout, and cross-platform audio still require a live lobby check.

Build with the normal project instructions. No third-party audio package or virtual device is required on listeners' devices.
