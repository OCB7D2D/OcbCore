# OCB Core Mod - 7 Days to Die (A20) Addon

This Core Mod is only meant as a POC and WIP project to kick-start
a community discussion to adapt something similar, or to build up on.
It should not be used in any project yet, since anything may change.

[![GitHub CI Compile Status][3]][2]

## Functionality Overview

This Mod is intended to be used as the very first mod to take care of all
other mods to be loaded. Goal is to do this as transparent and as agnostic
as possible, so no other mod needs to hard-depend on it if possible.

Currently there are three main features implemented in this POC.
- Load Orders of mods by dependencies, independent from Folder Path.
- Global configuration to conditionally enable Harmony Patches
- Conditional XML config patching (copied from my other mods)

## `ModConfig.xml` settings file

This Mod allows every other mod to have a `ModConfig.xml` file to
configure how the core mod should handle the loading.

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

Note: the algorithm to determine that order is not really battle-tested,
so expect that it might have bugs in some edge-cases. It should properly
detect circular dependencies though.

### Reasoning to interfere with load order

Today, you often will see issues because users are not really sure which
mods need which dependency (see SMX as an example). By enforcing this at
the core mod level, any other mod can say "I need ModFoo" or even "I never
want ModBar". In case "ModFoo" is missing, the error message will properly
say that "ModXY requires ModFoo, which is not installed". Further we could
add also a "not" check to say "ModXY doesn't like ModBar to be present".

### Assertions on ModConfig level

I've added another feature to run conditions when ModConfigs are loaded.
This way you can test conditions more easily and you can add more sanity
checks in anyway a mod author sees fit, e.g. testing for some other mod
to not be installed or of a certain version.

```xml
<ModConfig>
	<Assert condition="!ModOther" />
	<Assert condition="OcbCore&lt;1.0.0" />
</ModConfig>
```

Please beware that you can't write `<` and `>` directly in XML, you need
to escape it properly in order for the XML parser to not bail hard on you.
Or use unicode for "less/more or equal": `≤` (U+2264) and `≥` (U+2265).

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

## Custom Enums

Sometimes you want to add additional fields to existing enums, like
adding new Tile Entities or Quest Tags. It has been proven that mods
can already do that on their own account, but a helper utility would
certainly help to jump-start development of e.g. new Tile Entities.

```xml
<xml>
  <ModConfig>
    <Enum type="TileEntityType" name="MyTileEntity" />
    <Enum type="QuestTags" name="MyNewQuestTag" bitwise="True" />
  </ModConfig>
</xml>
```

### Some background on enums

Enums are merely a way to distinguish different variations of something,
like a shirt that comes in various predefined colors. Each color would
get a unique number (starting from 0), to identify it. That is basically
all that happens in the code. One part is to choose/assign an appropriate
number for each type. The other would be to act accordingly in the code,
regarding this which type a certain entity is. We only cover first part.

### Why are enums important for the game

Even though enums are merely integers, they still map to a given name.
This is most prominently used in the XML config files, where enums are
given by their name and not by their integer value. Via "reflection" the
game will determine what integer value a certain named enum has. E.g.
`EnumUtils.Parse<EnumGamePrefs>("LoadVanillaMap", false);`.

### How does it work

In order to "add" custom enum entries, we harmony patch two `System.Enum`
functions (namely `Parse` and `GetName`). Extending an existing enum still
involves some work, as we need to find an integer value we can occupy. Once
we figured that out, the integer value and the name is added to two maps.
One from integer to string, one from string to integer. We keep these maps
for all extended types, so the patched functions can additionally check
these maps to return the appropriate values for custom added enum entires.

### Future Ideas

There is some code that you always need to implement if you want to add a
custom `TileEntity`. First one is to choose an integer value that isn't used
yet. Then you need to make sure when Tile Entities are loaded, that you hook
some code into it, so it will instantiate the proper class when your integer
value is read from the save file. A core mode could add some support here.

### Potential issues

There is one main issue that will arise. Easiest to explain with a custom
`TileEntity`. Since the numbers we assign to new enums might be persisted in
a save file, it is prone to mod changes, e.g. when another mod is added
that also adds a custom `TileEntity`. The only real solution to this problem,
is to do the same thing the game already does with block IDs. It stores a list
of assigned ids when the game starts, and when new blocks are detected on a
later load in the future, it will deduct new ids for the new blocks from there.

## Custom EnumGamePrefs

On top of the custom enums, this mod allows to create new custom GamePrefs.
Currently in a very early POC state, but working for bool down to the UI.

### Setting custom game prefs via server settings xml

Using actual `GamePrefs` has the advantage, that they are saved by the game
automatically. In single player the user can configure those options via UI.
But for dedicated servers, these values are set via the "serversettings.xml".
Unfortunately dedicated servers load that file before applying any patches.
Therefore custom `GamePrefs` can't be supported directly there. But to solve
this I simply look for a "serversettings.core.xml", and if it exists, we load
that file too through the same parser, which now should know the new settings.

```xml
<?xml version="1.0"?>
<ServerSettings>
	<property name="LoadVanillaMap" value="false" />
</ServerSettings>
```

Note that the "serversettings.xml" path is given via `configfile` command
line option. We simply alter the extension, so you have a pair of files.
E.g. when you start the dedicated server with `configfile=MyServerConfig`,
it will load the following files.

- `MyServerConfig.xml` - Regular config before harmony patching
- `MyServerConfig.core.xml` - Optional config after enum patching

## Further ideas

### Inter Mod Communications (IMC)

Term borrowed from IPC (Inter Process Communications), to allow events
and other messages to be emitted and received in an agnostic way. E.g.
we only would provide a helper class, that relies on reflection, to
get to the shared API to register event handlers or to emit events.

This way a mod could e.g. inform other mods that a key-binding was changed. 

#### Checking if client side has all required mods installed

Core Mod must be installed on server and client side for this to work. Then
server Core Mod would send a list of mods required on the client side, which
the Core Mod on the client will check and enforce.

```xml
<ModConfig>
	<RequiredOn condition="server,client,both" />
</ModConfig>
```

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