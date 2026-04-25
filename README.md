# ClosedCaptions

A mod for Vintage Story which adds Closed Captions for currently-playing audio.

## Adding CC Support

If you'd like to add CC support to your own mod which has its own sounds or music, it's pretty straightforward!

(Please note that [JSON patching](https://wiki.vintagestory.at/index.php/Modding:JSON_Patching) is not enabled on Theme mods, only Content and Code.)

First, add a `.json` file to `assets/<yourmodid>/compatibility/closedcaptions/patches/` named whatever you like, with contents as follows:

```json
[
    {
        "op": "addmerge",
        "path": "/soundMap",
        "file": "closedcaptions:config/matchconfig.json",
        "value": [
            {
                "group": "yourmodid:sounds/*",
                "defaultKey": "yourmodid:default-key",
                "mappings": [
                    {
                        "match": "yourmodid:sounds/specific_sound.ogg",
                        "captionKey": "yourmodid:language-key",
                        // Lots of optional config, see below!
                    },
                    {
                        "match": "yourmodid:sounds/wildcard*",
                        "captionKey": "yourmodid:another-language-key",
                    },
                ]
            }
        ]
    }
]
```

#### A Note on Music

Music tracks are flagged as such by the engine, so don't require most of the optional fields that are available for standard audio clips.

### Required Fields

#### `group`

Wildcard that is used to determine if we should look in these `mappings` to find a match. In CC's base configuration, these are generally split out by folder.

#### `defaultKey`

Fallback language key to use if the `group` wildcard matched but there was not a more specific match found in `mappings`.

#### `match`

Optionally wildcard for specific `.ogg` files to match for this caption.

#### `captionKey`

Language key for this caption.

### Optional Fields

There are many optional fields to configure specific captions! These are all within individual mappings, beside `match` and `captionKey`.

#### `tags`

```json
{
    "match": "sounds/creature/bear/aggro.ogg",
    "captionKey": "closedcaptions:creature-bear-aggro",
    "tags": "animal,danger",
},
```

There a number of tags that are applied to captions to give players the ability to filter which captions they wish to see. If multiple tags are set on a caption, then the caption will be displayed as long as least one of the tags is enabled for the player. Some tags (noted by *) have additional player-configurable visual options.

* `ambience`*: Mood-setting sounds, like campfires or wind.
* `animal`: Includes wildlife and livestock, but also things like crickets and beehives. Often also tagged with `combat` for hurt noises and/or `danger` for particularly critical sounds (like bear growling or wolf howling).
* `block`: Sounds made by blocks being placed.
* `combat`: Sounds related to combat. This differs from `danger` in that it includes sounds like enemies getting hurt, or player blocking.
* `danger`*: Dangerous sounds! Landslides, creature aggro, nearby lightning... Anything that missing audio cues really puts the player in danger.
* `enemy`: Sounds made by enemies. Often also tagged wtih `combat` for hurt noises and/or `danger` for critical sounds.
* `environment`: Sounds made by the environment, like waves lapping, waterfalls, trees being felled, etc. A lot of overlap with `ambience`.
* `interaction`: Sounds from player interactions. Opening/closing chests and doors, placing items, etc.
* `machinery`: Operational machinery sounds. Chutes, querns, etc.
* `rust`*: Negative rust-themed sounds. Low temporal stability, rifts.
* `temporal`*: Positive temporal-themed sounds. Basically just translocators at the moment.
* `tool`: Sounds made by tools. Includes many sounds covered by `combat`, `interaction` and `machinery`.
* `voice`: [Harmonica murmuring]
* `walk`: Sounds from walking on or through blocks. (Very spammy if enabled.)
* `wearable`: Sounds from wearable items. Clanging from plate armor, rustling of leather, etc. (Also very spammy.)
* `weather`*: Weather sounds. Rain, thunder, etc.

There is also one music-specific tag.

* `event`: The music track is played as part of an event. There are moments where the swell of a music track is a really key part of the experience of an event, so ClosedCaptions has a specific player configuration to enable event music captions even if regular music captions are disabled.

#### `flags`

```json
{
    "match": "sounds/environment/creek.ogg",
    "captionKey": "closedcaptions:environment-creek",
    "flags": "directionless",
},
```

Only one optional flag is currently implemented.

* `directionless`: If set, the caption will always be displayed without direction indicators.

#### `group`

```json
{
    "match": "sounds/environment/rapids.ogg",
    "captionKey": "closedcaptions:environment-rapids",
    "tags": "environment",
    "group": { "name": "water", "priority" : 10 },
},
{
    "match": "sounds/environment/waterfall.ogg",
    "captionKey": "closedcaptions:environment-waterfall",
    "tags": "ambience,environment",
    "group": { "name": "water", "priority" : 5 },
},
```

Not to be confused with the match section `group`, this field tells ClosedCaptions to only display a single caption from within the specified group `name` based on the provided `priority`.

In the example above, if both `rapids.ogg` and `waterfall.ogg` are playing and both are visible based on player configuration, ClosedCaptions will prefer to display the caption for `rapids.ogg` and hide `waterfall.ogg`. (If `rapids.ogg` stops playing, then `waterfall.ogg` will display.)

#### `icon`

```json
{
    "match": "sounds/creature/drifter-idle*",
    "captionKey": "closedcaptions:creature-drifter-idle",
    "icon":{ "type": "item", "code": "creature-drifter-normal" },
},
```

Captions with an `icon` set will display the specified icon, using the format used by the [VTML](https://wiki.vintagestory.at/VTML) `<itemstack>` tag. (The [VTML Editor](https://mods.vintagestory.at/vtmleditor) mod is very helpful for figuring out what `type` and `code` to provide.)

Due to limitations steamming from how an audio clip is essentially disconnected from the entity source of that audio, there isn't currently a way to specify a better-matching icon for a particular instance of a caption. This is most noticible with creatures, where the same audio clip (eg `pig/hurt.ogg`) is played by many creatures with a different appearance (`creature-pig-eurasian-adult-male`, `creature-pig-redriver-adult-female`, etc), and we have to choose a single code to provide for the caption (in this case, `creature-pig-eurasian-adult-male`).

## Under the Hood

ClosedCaptions uses Harmony to hook into `LoadedSoundNative.Start()`, `LoadedSoundNative.SetVolume()` and `LoadedSoundNative.Stop()`.

It might be possible to hook into `World.PlaySoundAt()` / `World.PlaySoundFor()` instead to be able to get entity information for icons, and to be able to differentiate things such as bloomeries and _omg there is a wildfire burning out of control_ (which both use the same audio clip).

With more access to deeper levels of the engine, though, it would be lovely to be able to have the `Entity` source of an audio clip tracked with the clip and be able to access it directly. This would also allow audio clips to update their position to match their source entity. (It's much more obvious with directional closed captions that an audio clip remains stationary even when the entity that played it is moving quickly.)

***

> (Mostly unrelated... many audio clips are played with hardcoded `AssetLocation`s, which is really surprising given how data-driven the vast majority of the game is. If someone wanted to, say, have bloomeries play a different audio clip file so that it could have a different caption than _omg there is a wildfire burning out of control_, they aren't really able to because the sound is hardcoded in [`BlockEntityBloomery.startSound()`](https://github.com/anegostudios/vssurvivalmod/blob/b1de40fe54deefe827b31e564c993d43a7ac2b15/BlockEntity/BEBloomery.cs#L144). Hypothetically. 😉)