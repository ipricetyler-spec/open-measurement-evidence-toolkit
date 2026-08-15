# Security Policy

## Supported version

Security fixes currently target the latest commit on the default branch. Formal version support begins with the first tagged release.

## Threat boundary

The toolkit treats manifests, settings snapshots, paths, and CSV evidence as untrusted input. It applies size, depth, count, encoding, path-containment, and consistency checks before analysis. It performs no network access, telemetry upload, system tuning, privilege elevation, game injection, process capture, registry mutation, BIOS/firmware access, or anti-cheat interaction.

The Windows held-file checks reduce time-of-check/time-of-use and link/reparse-point risks for contained local evidence. They do not prove that upstream capture software or the measured system is trustworthy.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Use GitHub's private vulnerability reporting feature when it is enabled for this repository. If that feature is unavailable, contact the maintainer through the security contact published on the maintainer's GitHub profile.

Include affected version or commit, reproduction steps, impact, and any suggested mitigation. Please avoid accessing data or systems you do not own or have permission to test.

