# Defenders-of-the-Dune  
Matthew Diamond's programming contributions to **Defenders of the Dune**, an **RTS game** made by **WolverineSoft Studio** using **Unity** and **C#**.  

Here is a link to the game!  
[**Defenders of the Dune on Steam**](https://store.steampowered.com/app/3394870/Defenders_of_the_Dune/)  

Here are some of **Matthew Diamond's contributions** that were integral to the project. This code was written in 2024.

**`AnchorBuildingManager.cs`**: Implemented **healing mechanics** for friendly units within range and manages **range indicator visualization**. Handles periodic healing during the **Day Phase** and dynamically toggles indicators during building interactions. Includes **outpost lifecycle management** to ensure proper initialization and cleanup when buildings are destroyed.

**`BuildingManager.cs`**: Manages **building placement validation**, including checking **proximity to outposts** and ensuring placement on valid terrain. Implemented the **building decay system**, which gradually reduces building health when not within range of an outpost. Oversees **health bar rendering**, **construction progression**, and **damage delegation** across linked structures.

**`BuildingPlacer.cs`**: Handles the **placement and rotation** of buildings during construction. Includes logic for **visualizing building placement validity**, detecting mouse input for building rotation, and enforcing **placement constraints**. Manages **range indicator toggling** for anchor buildings during the placement phase.

**`Building.cs`**: Represents individual **building units**, handling core functionalities such as **construction progression**, **health updates**, and **material adjustments** based on placement validity. Includes logic for **building decay triggers**, **resource management** during repairs, and **event publishing** upon construction completion or destruction.

**`CutsceneTransitionManager.cs`**: Implemented **smooth cutscene transitions** during gameplay, ensuring seamless visual and audio transitions between gameplay phases. Allows players to skip cutscenes.
