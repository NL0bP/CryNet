# Notices

CryPhysics.Native contains original wrapper code from the ArcheAze contributors and is intended for Apache-2.0 distribution.

The native backend builds against Amazon Lumberyard CryPhysics, CryCommon, and AzCore sources. Those files retain their upstream copyright notices:

> All or portions of this file Copyright (c) Amazon.com, Inc. or its affiliates or its licensors.

When publishing this as a standalone repository, keep the upstream Lumberyard license text alongside any vendored Lumberyard source, or prefer the provided sparse-checkout scripts so contributors fetch upstream source directly.

Recommended public-repo policy:

- Commit the wrapper/facade code, CMake, stubs, and fetch scripts.
- Do not commit local `build/`, `bin/`, `.lumberyard-tmp/`, `obj/`, or generated IDE files.
- Either omit `lumberyard-src/`, `crycommon-min/`, and `azcore-src/` and let scripts recreate them, or include upstream license/notice files if vendoring them.
