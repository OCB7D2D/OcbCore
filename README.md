# OCB Core Mod - 7 Days to Die (A20) Addon

This Core Mod is only meant as a POC and WIP project to kick-start
a community discussion to adapt something similar, or to build up on.
It should not be used in any project yet, since anything my change.

[![GitHub CI Compile Status][3]][2]

## Functionality Overview

This Mod is intended to be used as the very first mod to take care
of all other mods to be loaded. Goal is to do this transparently as
possible, so no other mod needs to hard-depend on it if possible.

Currently there are three main features implemented in this POC.
- Load Orders of mods by dependencies, independent from Folder Path.
- Global configuration to conditionally enable Harmony Patches
- Conditional XML config patching (copied from my other mods)

## `ModConfig.xml` settings file

This Mod allows every other mod to have a `ModConfig.xml` file to
configure how the code mod should handle the loading.

```xml
<xml>
	<ModConfig>
		<Require mod="OcbCore" />
		<After mod="ModFoo" />
		<Config name="SCore" value="Fire"/>
	</ModConfig>
</xml>
```

This core mod will defer loading of all further mods and then
applies the proper dependencies to get the correct load order.
If you `Require` a mod, the core mod will throw an error if said
mod is not found, `After` is conditional and doesn't change the
load order, if the mod dependency is not found.

### Load Order

There are two main parts where the load order is relevant. First
when the game start, all mods are loaded and initialized. This
defines in which order Harmony patches are applied and executed.
Secondly, once a world is being loaded, the game also loads the
XML config in that same load order.

Note: the algorithm to determine that order is not really battle-
tested, so expect that it might have bugs in some edge-cases. It
should properly detect circular dependencies though.

## Conditional Harmony Patching

Sometimes some mods contain multiple functionalities that should
only be enabled if some other mods relies on it (e.g. SCore). In
order to supports that, the requester mod can set a global config key,
that the provider mod can query in its harmony patches:

```cs
[HarmonyCondition("HasConfig(SCore;Fire)")]
[HarmonyCondition("OcbCore>=0.0.0,OcbCore<=0.0.1")]
```

In order to make this new annotation known, the provider mod must
include [Library/HarmonyCondition.cs](Library/HarmonyCondition.cs).
I've taken care to decouple most stuff from this core mod, so you
really only need that single file. In case this core mod is not found,
you can specific in that file, if you want to blindly accept or reject
all Harmony Patches with the `HarmonyCondition` annotation.

Multiple Conditions on the same Harmony Patch act like an OR group.
To AND multiple conditions, use a comma separated list. Syntax is
the same as for the condition XML patching.

## Conditional XML patching

The XML patcher has been extended to allow conditional blocks (only
on the main level) and, for easier management, to include additional
XML files. Also works on servers, since it sends the final results.

```xml
<configs patcher-version="2">
    <modif condition="UndeadLegacy_CoreModule">
        <include path="recipes.ulm.xml"/>
    </modif>
    <modelsif condition="Darkness Falls Core">
        <include path="recipes.a20.xml"/>
        <include path="recipes.df.xml"/>
    </modelsif>
    <modelse>
        <include path="recipes.a20.xml"/>
    </modelse>
</configs>
```

In case you test for other mods, you should always include those
in the `ModConfig.xml` as an `After` dependency to ensure the
proper load order under all circumstances.

## Download and Install

Simply [download here from GitHub][1] and put into your A20 Mods folder:

- https://github.com/OCB7D2D/OcbCore/releases (master branch)

## Changelog

### Version 0.0.0

- Initial working version

## Compatibility

I've developed and tested this Mod against version a20.6 (b8).

[1]: https://github.com/OCB7D2D/OcbCore/releases
[2]: https://github.com/OCB7D2D/OcbCore/actions/workflows/ci.yml
[3]: https://github.com/OCB7D2D/OcbCore/actions/workflows/ci.yml/badge.svg