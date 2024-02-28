# Suit Log by Damián Garro

![thumbnail](images/thumbnail.webp)

Tired of having to go to the ship again and again just to check any data in the Ship Log? Not anymore! With Suit Log you just need to **suit up**, go out to have your adventures without annoying interruptions and check the data you want at any time!

Demo video: https://www.youtube.com/watch?v=Xpf7Rwy12Bk

You don't have to learn anything to use this new interface, the controls are the same as in the Ship Log! Just use the autopilot key to open it (**X key** in the keyboard or **up D-pad button** in the gamepad by default).  You'll have everything you need right there: entries with their photos, "icons", marking location in HUD...

And all of this displayed in a diegetic interface that shares design with the other HUD elements displayed on the helmet, intended to feel like a vanilla feature and not an external add-on!

![poem|width=400px](images/poem.jpg)

*Note:* Only Map Mode is included in Suit Log, you will still need the to use the Ship Log for the Rumor Mode. The "map" is different here though, the planets (and other locations) are displayed as a list, where you can select one to view the list of its entries. The vanilla Slide Reel Player isn't available in the Suit Log, but you can install the [Ship Log Slide Reel Player Plus](https://outerwildsmods.com/mods/shiplogslidereelplayerplus/) and it would be includes as a Suit Log Mode! Yes, similarly to [Custom Ship Log Modes](https://outerwildsmods.com/mods/customshiplogmodes/) allow other mods to create modes for the Ship Log, developers can also create them for the Suit Log.

For the regular Suit Log mode, entries you read will be marked as read the same way the Ship Log does it, although it is planned to make this optional in a future update. Other future updates could include the option of pausing the game while using the Suit Log. Modes are selected in the same way, including a mode selector.

If you want to use the Suit Log in the few places where you can't have the Suit on, I recommend using the [Cheat And Debug Menu](https://outerwildsmods.com/mods/cheatanddebugmenu/) mod, pressing the **F2 key** to put on the suit, although I may add the option to be able to open the Suit Log even without the suit  as a *cheat mode*.

Please, I'd be happy to receive any suggestions and bug reports on [GitHub](https://github.com/dgarroDC/SuitLog/issues) or in the [Outer Wilds Modding Discord Server](https://discord.gg/9vE5aHxcF9). Thanks.

## Compatibility and interactions with other mods

* [Archaeologist Achievement Helper](https://outerwildsmods.com/mods/archaeologistachievementhelper/): The mods are compatible and if you run the game with Suit Log and this helper mod, the later would add the *"There's more to explore here."* mark and text to entries to both the Ship Log and Suit Log. However, the *"Show all missing facts"* optional feature only applies to the Ship Logs and the Suit Log would be unaffected by this.
* [New Horizons](https://outerwildsmods.com/mods/newhorizons/): The mods are fully compatible, opening the Suit Log shows planets added by addons of New Horizons. The menu list only displays the planets of the current star system (this is the same behaviour that the Ship Log has in New Horizons). Entries added to the vanilla Outer Wilds planets are also listed. The Interstellar Mode that New Horizons adds to the Ship Log doesn't currently have a Suit Log Mode counterpart.

## Suit Log Modes Developer Guide

This guide assumes that you are already familiarized with the [Custom Ship Log Modes](https://outerwildsmods.com/mods/customshiplogmodes/), as developing Suit Log modes is very similar.

The Suit Log API includes the same `AddMode` method to add modes, including the `isEnabledSupplier` and `nameSupplier` parameters. Not only that, the Suit Log modes are actually represented by `ShipLogMode` objects too. Even if they aren't used in the Ship Log and the name could be misleading, using that already existing base class makes thing easier to port. If your custom mode is simple enough, and you mostly just use the "item list" UI (more on that later), you may even use your same class for both the Ship Log and the Suit Log (or with only some minor adjustments)!

Methods from the `ShipLogMode` class are pretty much used the same as in the context of the Ship Log. Of course, the prompt lists passed to the `Initialize` method aren't the ones that are displayed in the Ship Log screen. Instead, they are the regular ones that appear on screen: `centerPromptList` is the same that you would get using `Locator.GetPromptManager().GetScreenPromptList(PromptPosition.BottomCenter)` (note that it isn't `PromptPosition.Center`, because in the Ship Log the `centerPromptList` is actually at the bottom center) and `upperRightPromptList` is `Locator.GetPromptManager().GetScreenPromptList(PromptPosition.UpperRight)`. And the `OnEnterComputer`/`OnExitComputer` methods of your Suit Log modes, despite their names, are called when the Suit Log is opened or closed.

Besides the `AddMode` method, the rest of the API methods are all dedicated to item lists, all starting with `ItemList`. These are mostly the same as in Custom Ship Log Modes with some differences:
* The `ItemListMake` doesn't take parameters to indicate the usage of photo or description fields. This is because the Suit Log item list UI is formed by "floating" elements, instead of being different rectangles divisions like in the Ship Log. You can still hide/show the photo and question mark text as you wish like in the Ship Log, and see the next point for the description field (there is an important API difference):
* Two additional methods are included in the API: `ItemListDescriptionFieldOpen` and `ItemListDescriptionFieldClose`. You can use them to open or close the description field. It starts closed, and if you open it and don't close it, it will open automatically when the item list is opened. You can even call the method to open the description field without having the item list opened. That means that, if you want to always display the description field, you could just open it after creating the item list and then forget about these two methods. See the Ship Log Slide Reel Player Plus's code example bellow that does exactly that!
* The upper right prompt list's position is lowered if the photo or the question mark text object is active to avoid overlapping with them (this is the same thing that happens when you take a snapshot with your probe in the base game, the prompts are also lowered),. Make sure to hide/show this objects using the `SetActive` method, they are hidden by the default, although you are probably already doing that for your Ship Log mode.
* Up to **10** items are displayed at once (that can be scrolled), while in Ship Log up to **7** or **14** elements can be displayed depending if the description field is used (in Suit Log, there is no difference if you open or close it, the list itself has always the same size). So the list returned by `ItemListGetItemsUI` has **10** elements. You probably don't have to worry about this, it's just a presentation detail.
* Each item list has its own description field object, it isn't shared by all lists like in Custom Ship Log Modes, so you know that others won't be touching yours.
* The methods `ItemListMarkHUDRootEnable` and `ItemListMarkHUDGetPromptList` that are in Custom Ship Log Modes, aren't included in the Suit Log's API. This is because that prompt list is an specific UI element for the Ship Log's item lists.

Like it Custom Ship Log Modes, it's recomended to use an item list's wrapper to avoid having to pass the item list to all the API methods. Suit Log includes a [ItemListWrapper.cs](SuitLog/API/ItemListWrapper.cs) that you could copy and use (the main Suit Log mode and the mode selector mode use it). However, if you are creating a mode that it's used for both the Ship Log and the Suit Log, it's probably better to use a generic wrapper for both. In that case, feel free to copy this other [ItemListWrapper.cs](https://github.com/dgarroDC/ShipLogSlideReelPlayer/blob/main/ShipLogSlideReelPlayer/CustomModesAPIs/ItemListWrapper.cs) from the Ship Log Slide Reel Player Plus mod. This particular `ItemListWrapper` has two subclasses, `ShipLogItemListWrapper` and `SuitLogItemListWrapper`, so you could create those two and then in your mode just reference a generic `ItemListWrapper`, calling its methods without worrying too much about the exact implementation. This is how that mod creates both item lists and modes:
```csharp
ICustomShipLogModesAPI customShipLogModesAPI = ModHelper.Interaction.TryGetModApi<ICustomShipLogModesAPI>("dgarro.CustomShipLogModes");
customShipLogModesAPI.ItemListMake(true, true, itemList =>
{
    SlideReelPlayerMode reelPlayerMode = itemList.gameObject.AddComponent<SlideReelPlayerMode>();
    reelPlayerMode.itemList = new ShipLogItemListWrapper(customShipLogModesAPI, itemList); 
    reelPlayerMode.gameObject.name = nameof(SlideReelPlayerMode);
    customShipLogModesAPI.AddMode(reelPlayerMode, () => true, () => SlideReelPlayerMode.Name);
});

// Optional Suit Log dependency, so use the ? operator:
ISuitLogAPI suitLogAPI = ModHelper.Interaction.TryGetModApi<ISuitLogAPI>("dgarro.SuitLog");
suitLogAPI?.ItemListMake(itemList =>
{
    SlideReelPlayerMode reelPlayerMode = itemList.gameObject.AddComponent<SlideReelPlayerMode>();
    SuitLogItemListWrapper wrapper = new SuitLogItemListWrapper(suitLogAPI, itemList);
    wrapper.DescriptionFieldOpen(); // Always keep this open!
    reelPlayerMode.itemList = wrapper; 
    reelPlayerMode.gameObject.name = nameof(SlideReelPlayerMode);
    suitLogAPI.AddMode(reelPlayerMode, () => true, () => SlideReelPlayerMode.Name);
});
```

Note that both modes use the same [`SlideReelPlayerMode`](https://github.com/dgarroDC/ShipLogSlideReelPlayer/blob/main/ShipLogSlideReelPlayer/SlideReelPlayerMode.cs) class, and it just assigns the `ShipLogItemListWrapper`/`SuitLogItemListWrapper` to its `itemList` field, because that field has the generic `ItemListWrapper` class as the type. And the `SlideReelPlayerMode` class is really agnostic about the fact whether if its used in the Ship Log or the Suit Log, so it doesn't have to worry about that detail. However, if your custom Ship Log mode does more custom UI things, you may need to take more work when porting it to the Suit Log.

In the code above, also note the call to `DescriptionFieldOpen` that it makes for the Suit Log mode case just once, like mentioned earlier. Then in both cases of the mode the description field is always open.

Please don't hesitate to ask me for help if you want to port your custom Ship Log mode to the Suit Log!
