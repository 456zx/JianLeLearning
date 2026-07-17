# C++ 部分

### 1. 模块结构

- `ActionRPG.cpp` 使用 `FDefaultGameModuleImpl` 注册主游戏模块，并定义 `LogActionRPG` 日志分类。
- `ActionRPG.h` 集中引入项目通用的 Engine、复制和 `RPGTypes` 头文件，并声明日志分类。
- `ActionRPGLoadingScreen` 使用 `IMPLEMENT_GAME_MODULE` 注册独立游戏模块；该模块需要在 `.uproject` 中以 `PreLoadingScreen` 阶段加载。

### 2. 数据基础：物品、槽位、存档结构

#### `FRPGItemSlot`

- `ItemType`：`FPrimaryAssetType`，限定槽位允许放入的物品类型。
- `SlotNumber`：`int32`，从 `0` 开始的槽位编号；默认构造时为 `-1`。
- 带参构造函数写入 `ItemType` 和 `SlotNumber`。
- `operator==` 同时比较类型和编号；`operator!=` 返回相反结果。
- `GetTypeHash` 组合 `ItemType` 与 `SlotNumber` 的哈希，因此该结构可以作为 `TMap` 和 `TSet` 的键。
- `IsValid()` 仅在 `ItemType.IsValid()` 且 `SlotNumber >= 0` 时返回 `true`。

#### `FRPGItemData`

- `ItemCount`：`int32`，库存数量；默认构造时为 `1`。
- `ItemLevel`：`int32`，该物品全部实例共享的等级；默认构造时为 `1`。
- `operator==` 同时比较数量和等级；`operator!=` 返回相反结果。
- `IsValid()` 在 `ItemCount > 0` 时返回 `true`。
- `UpdateItemData(Other, MaxCount, MaxLevel)`：
  - `MaxCount <= 0` 时把数量上限按 `MAX_int32` 处理。
  - `MaxLevel <= 0` 时把等级上限按 `MAX_int32` 处理。
  - 新数量为 `ItemCount + Other.ItemCount`，再裁剪到 `[1, MaxCount]`。
  - 新等级直接采用 `Other.ItemLevel`，再裁剪到 `[1, MaxLevel]`。

#### 库存与存档委托

- `FOnInventoryItemChanged` / `FOnInventoryItemChangedNative`：传递 `bAdded` 和 `URPGItem*`。
- `FOnSlottedItemChanged` / `FOnSlottedItemChangedNative`：传递 `FRPGItemSlot` 和 `URPGItem*`。
- `FOnInventoryLoaded` / `FOnInventoryLoadedNative`：通知整份库存已经重新加载。
- `FOnSaveGameLoaded` / `FOnSaveGameLoadedNative`：传递当前 `URPGSaveGame*`。

#### `IRPGInventoryInterface`

- `URPGInventoryInterface` 是 Unreal 反射系统使用的 `UInterface` 包装类，带 `CannotImplementInterfaceInBlueprint` 限制；原生类实现 `IRPGInventoryInterface`。
- 该原生接口不能由 Blueprint 实现。
- `GetInventoryDataMap()` 返回 `URPGItem* -> FRPGItemData` 映射。
- `GetSlottedItemMap()` 返回 `FRPGItemSlot -> URPGItem*` 映射。
- `GetInventoryItemChangedDelegate()`、`GetSlottedItemChangedDelegate()`、`GetInventoryLoadedDelegate()` 返回对应的原生委托引用。
- `ARPGCharacterBase` 通过该接口读取库存和监听槽位变化，不需要转换到具体玩家控制器类。

#### `URPGSaveGame`

- 存档版本依次为 `Initial`、`AddedInventory`、`AddedItemData`；构造时把 `SavedDataVersion` 设为 `LatestVersion`。
- `InventoryData`：`FPrimaryAssetId -> FRPGItemData`，保存物品数量和等级。
- `SlottedItems`：`FRPGItemSlot -> FPrimaryAssetId`，保存各槽位对应的物品资产 ID。
- `UserId`：`FString`，保存用户唯一标识。
- `InventoryItems_DEPRECATED`：旧版本使用的物品 ID 数组，只用于读取迁移。
- `SavedDataVersion`：记录该存档写入时使用的版本。
- `Serialize()`：加载非最新版本存档时，如果版本早于 `AddedItemData`，把 `InventoryItems_DEPRECATED` 中每个 ID 转成数量 `1`、等级 `1` 的 `FRPGItemData` 加入 `InventoryData`，清空旧数组，最后把版本更新为 `LatestVersion`。

### 3. 物品系统：资产即物品定义

- `URPGItem`
  - 是所有物品的基类，继承 `UPrimaryDataAsset`。
  - `ItemType`：`FPrimaryAssetType`，物品资产类型，由原生子类设置；蓝图只读。
  - `ItemName`：`FText`，显示给用户的短名称。
  - `ItemDescription`：`FText`，显示给用户的详细描述。
  - `ItemIcon`：`FSlateBrush`，物品图标。
  - `Price`：`int32`，物品价格，构造时默认为 `0`。
  - `MaxCount`：`int32`，库存最大数量，构造时默认为 `1`；小于等于 `0` 表示无限数量。
  - `MaxLevel`：`int32`，物品最大等级，构造时默认为 `1`；小于等于 `0` 表示无限等级。
  - `GrantedAbility`：`TSubclassOf<URPGGameplayAbility>`，物品放入槽位时授予的能力。
  - `AbilityLevel`：`int32`，物品授予的能力等级，构造时默认为 `1`；小于等于 `0` 时使用角色等级。
  - `IsConsumable()`：当 `MaxCount <= 0` 时返回 `true`，否则返回 `false`。
  - `GetIdentifierString()`：返回 `GetPrimaryAssetId().ToString()`。
  - `GetPrimaryAssetId()`：返回 `FPrimaryAssetId(ItemType, GetFName())`；其中 `GetFName()` 是 DataAsset 的资源对象名，不是 `ItemName`。
- `URPGPotionItem`
  - 原生把 `ItemType` 固定为 `Potion`。
- `URPGSkillItem`
  - 原生把 `ItemType` 固定为 `Skill`。
- `URPGTokenItem`
  - 原生把 `ItemType` 固定为 `Token`，并把 `MaxCount = 0`，表示无限堆叠。
- `URPGWeaponItem`
  - 原生把 `ItemType` 固定为 `Weapon`。
  - 多了一个 `WeaponActor` 字段，用来定义生成哪种武器 Actor。

### 4. 资产管理

- `URPGAssetManager`
  - `PotionItemType`、`SkillItemType`、`TokenItemType`、`WeaponItemType` 分别固定为 `Potion`、`Skill`、`Token`、`Weapon`。
  - `Get()` 把 `GEngine->AssetManager` 转换为 `URPGAssetManager`；配置类型不正确时记录 Fatal 日志。
  - `StartInitialLoading()` 先调用父类实现，再执行 `UAbilitySystemGlobals::InitGlobalData()`。
  - `ForceLoadItem(PrimaryAssetId, bLogWarning)` 通过 `GetPrimaryAssetPath` 取得软路径，再调用 `TryLoad()` 同步加载并转换为 `URPGItem`。
  - 同步加载可能造成卡顿，函数不额外持有返回物品的引用；加载失败且 `bLogWarning=true` 时记录物品 ID。

### 5. 存档与背包

#### `URPGGameInstanceBase`

- 构造时把 `SaveSlot` 设为 `SaveGame`，把 `SaveUserIndex` 设为 `0`。
- `DefaultInventory`：`FPrimaryAssetId -> FRPGItemData`，新存档需要具备的默认物品。
- `ItemSlotsPerType`：`FPrimaryAssetType -> int32`，配置每种物品类型的槽位数量。
- `SaveSlot` 和 `SaveUserIndex`：传给 GameplayStatics 存取函数的槽位名和平台用户索引。
- `CurrentSaveGame`：当前内存中的 `URPGSaveGame`。
- `bSavingEnabled`：控制是否从磁盘读写，初始为 `false`；关闭时所有加载都按新存档处理，`WriteSaveGame()` 返回 `false`。
- `bCurrentlySaving`：初始为 `false`，标记异步保存正在执行。
- `bPendingSaveRequested`：初始为 `false`，异步保存期间再次收到保存请求时置为 `true`。
- `OnSaveGameLoaded` 和 `OnSaveGameLoadedNative`：存档加载或重置完成后分别通知 Blueprint 和原生监听者。
- `AddDefaultInventory(SaveGame, bRemoveExtra)`：
  - `bRemoveExtra=true` 时先清空存档中的 `InventoryData`。
  - 遍历 `DefaultInventory`，只添加存档中尚不存在的物品，不覆盖已有数量和等级。
- `IsValidItemSlot(ItemSlot)`：先调用结构体自身的 `IsValid()`；再从 `ItemSlotsPerType` 查类型，只有 `SlotNumber < 配置数量` 时返回 `true`。
- `GetCurrentSaveGame()`：返回 `CurrentSaveGame`，修改该对象不会自动写盘。
- `SetSavingEnabled(bEnabled)`：直接修改 `bSavingEnabled`。
- `LoadOrCreateSaveGame()`：仅在磁盘存档存在且保存已启用时同步调用 `LoadGameFromSlot`；随后统一交给 `HandleSaveGameLoaded`，返回值表示读到了旧档还是创建了新档。
- `HandleSaveGameLoaded(SaveGameObject)`：
  - 保存关闭时忽略传入对象。
  - 能转换为 `URPGSaveGame` 时设为 `CurrentSaveGame`，补入缺失的默认物品并返回 `true`。
  - 转换失败时创建新的 `URPGSaveGame`，清空后写入默认物品并返回 `false`。
  - 两条分支最后都广播 Blueprint 和原生存档加载委托。
- `GetSaveSlotInfo()`：输出当前 `SaveSlot` 和 `SaveUserIndex`。
- `WriteSaveGame()`：
  - 保存关闭时返回 `false`。
  - 没有正在保存时设置 `bCurrentlySaving=true`，调用 `AsyncSaveGameToSlot` 并返回 `true`。
  - 已在保存时只设置 `bPendingSaveRequested=true`，同样返回 `true`。
- `ResetSaveGame()`：用空对象调用 `HandleSaveGameLoaded(nullptr)`，在内存中创建并广播一份默认存档；不会立即写盘。
- `HandleAsyncSave()`：清除 `bCurrentlySaving`；存在挂起请求时清除标记并再次调用 `WriteSaveGame()`，多次并发请求只合并为一次补充保存。

#### `ARPGPlayerControllerBase`

- 继承 `APlayerController` 并实现 `IRPGInventoryInterface`。
- `InventoryData`：`URPGItem* -> FRPGItemData`，保存拥有的物品、数量和等级。
- `SlottedItems`：`FRPGItemSlot -> URPGItem*`，保存各槽位当前物品。
- `OnInventoryItemChanged`、`OnSlottedItemChanged`、`OnInventoryLoaded` 是 Blueprint 多播委托；同时保存对应的 Native 委托。
- `InventoryItemChanged` 和 `SlottedItemChanged` 是通知委托后调用的 BlueprintImplementableEvent。
- `AddInventoryItem(NewItem, ItemCount, ItemLevel, bAutoSlot)`：
  - 物品为空或数量/等级小于等于 `0` 时记录警告并返回 `false`。
  - 读取旧数据，通过 `FRPGItemData::UpdateItemData` 累加数量并用新等级覆盖旧等级；数据改变时写回并发送添加通知。
  - `bAutoSlot=true` 时调用 `FillEmptySlotWithItem`，即使物品数据已达到上限仍会尝试补槽。
  - 数据或槽位任一发生变化时调用 `SaveInventory()` 并返回 `true`。
- `RemoveInventoryItem(RemovedItem, RemoveCount)`：
  - 物品为空或库存中不存在时返回 `false`。
  - `RemoveCount <= 0` 时把数量直接设为 `0`；否则减去指定数量。
  - 剩余数量大于 `0` 时只更新数据；否则从 `InventoryData` 删除，并清空所有引用该物品的槽位。
  - 发送移除通知，调用 `SaveInventory()`，返回 `true`。
- `GetInventoryItems(Items, ItemType)`：把指定类型的库存物品追加到输出数组；`ItemType` 无效时输出全部类型，函数不会先清空数组。
- `GetInventoryItemCount(Item)`：找到数据时返回 `ItemCount`，否则返回 `0`。
- `GetInventoryItemData(Item, ItemData)`：找到时复制数据并返回 `true`；找不到时输出数量 `0`、等级 `0` 并返回 `false`。
- `SetSlottedItem(ItemSlot, Item)`：
  - 遍历已有槽位，找到目标键后写入物品并通知。
  - 同一非空物品存在于其他槽位时清空旧槽并通知。
  - 仅当目标槽位键已经存在时保存并返回 `true`；实现中不额外校验物品是否在库存内或类型是否匹配。
- `GetSlottedItem(ItemSlot)`：返回槽位中的物品；键不存在时返回 `nullptr`。
- `GetSlottedItems(Items, ItemType, bOutputEmptyIndexes)`：遍历并追加匹配类型的槽位值；`ItemType` 无效时输出全部类型。当前实现没有读取 `bOutputEmptyIndexes`，因此匹配槽位的空值也会加入数组。
- `FillEmptySlotWithItem(NewItem)`：
  - 只检查与物品 Primary Asset Type 相同的槽位。
  - 物品已在任一同类型槽位中时返回 `false`。
  - 否则选择编号最小的空槽，写入物品并发送槽位通知。
- `FillEmptySlots()`：对库存中每个物品调用 `FillEmptySlotWithItem`；任一槽位变化后保存一次。
- `SaveInventory()`：
  - 取得 `URPGGameInstanceBase` 和当前存档，任一无效时返回 `false`。
  - 清空存档中的库存和槽位映射；非空库存物品转成 Primary Asset ID 写入。
  - 所有槽位都会写入存档，空槽使用无效的 `FPrimaryAssetId`。
  - 调用 `GameInstance->WriteSaveGame()` 后返回 `true`；不使用该调用的返回值决定自身结果。
- `LoadInventory()`：
  - 先清空控制器的库存和槽位；GameInstance 无效时返回 `false`。
  - 首次调用时绑定 `OnSaveGameLoadedNative`，之后存档重置或重载会再次调用 `LoadInventory()`。
  - 按 `ItemSlotsPerType` 创建全部空槽。
  - 通过 `URPGAssetManager::ForceLoadItem` 把存档资产 ID 还原为物品指针；只有槽位有效且物品加载成功时才恢复槽位。
  - 一个有效已保存槽位都没有时调用 `FillEmptySlots()` 自动装槽。
  - 无论加载成功与否都发送库存加载完成通知；有当前存档时返回 `true`，否则返回 `false`。
- `NotifyInventoryItemChanged`：依次广播 Native 委托、Blueprint 多播委托，再调用 `InventoryItemChanged`。
- `NotifySlottedItemChanged`：依次广播 Native 委托、Blueprint 多播委托，再调用 `SlottedItemChanged`。
- `NotifyInventoryLoaded`：依次广播 Native 和 Blueprint 库存加载委托。
- `HandleSaveGameLoaded()`：收到 GameInstance 通知后重新调用 `LoadInventory()`。
- `BeginPlay()`：先调用 `LoadInventory()`，再调用父类 `BeginPlay()`。

### 6. 角色、能力、属性：GAS 主入口

#### `ARPGCharacterBase`

- 继承 `ACharacter`，实现 `IAbilitySystemInterface` 和 `IGenericTeamAgentInterface`。
- 构造时创建可复制的 `URPGAbilitySystemComponent` 和 `URPGAttributeSet`；`CharacterLevel=1`，`bAbilitiesInitialized=false`。
- `CharacterLevel`：可复制的角色等级。
- `GameplayAbilities`：创建角色时授予、不绑定具体输入槽位的能力类数组。
- `DefaultSlottedAbilities`：`FRPGItemSlot -> AbilityClass` 的默认槽位能力。
- `PassiveGameplayEffects`：初始化时应用到自身的被动 Gameplay Effect 类数组。
- `AbilitySystemComponent`、`AttributeSet`：角色持有的 GAS 组件和属性集。
- `InventorySource`：实现 `IRPGInventoryInterface` 的库存来源。
- `SlottedAbilities`：`FRPGItemSlot -> FGameplayAbilitySpecHandle`，记录各槽位已授予的能力句柄。
- `GetAbilitySystemComponent()` 返回角色持有的 ASC。
- `GetHealth()` 在 `AttributeSet` 为空时返回 `1`，否则返回生命值；`GetMaxHealth()`、`GetMana()`、`GetMaxMana()`、`GetMoveSpeed()` 直接读取属性集。
- `GetCharacterLevel()` 返回 `CharacterLevel`。
- `SetCharacterLevel(NewLevel)`：仅在新等级大于 `0` 且与当前值不同时执行；先移除启动能力和效果，写入新等级，再重新授予，成功时返回 `true`。
- `PossessedBy(NewController)`：
  - 先调用父类实现。
  - 尝试把新 Controller 转成库存接口；自动战斗临时换成不提供库存的 AI Controller 时，继续使用仍然有效的旧 `InventorySource`。
  - 对库存源绑定槽位变化和库存加载委托。
  - 调用 `InitAbilityActorInfo(this, this)`，再调用 `AddStartupGameplayAbilities()`。
- `UnPossessed()`：从库存源移除两个委托并清空句柄，但保留 `InventorySource`，供自动战斗临时切换控制器后继续使用。
- `OnRep_Controller()`：Controller 在客户端复制变化后调用 `RefreshAbilityActorInfo()`。
- `GetLifetimeReplicatedProps()`：复制 `CharacterLevel`。
- `AddStartupGameplayAbilities()`：
  - 只在 Authority 且尚未初始化时执行。
  - 以角色等级授予 `GameplayAbilities`，SourceObject 为角色自身。
  - 为每个 `PassiveGameplayEffects` 创建 Context，把角色设为 SourceObject，生成同等级 Spec 并应用到自身 ASC。
  - 调用 `AddSlottedGameplayAbilities()`，最后设置初始化标记。
- `RemoveStartupGameplayAbilities()`：
  - 只在 Authority 且已经初始化时执行。
  - 清除 SourceObject 为当前角色且类存在于 `GameplayAbilities` 中的能力。
  - 按 `EffectSource=this` 移除当前角色施加的全部活动效果。
  - 强制移除全部槽位能力并清除初始化标记。
- `FillSlottedAbilitySpecs()`：
  - 先按角色等级生成 `DefaultSlottedAbilities`。
  - 再遍历库存槽位；物品类型名为 `Weapon` 时直接使用物品的 `AbilityLevel`，其他类型使用角色等级。
  - 物品和 `GrantedAbility` 有效时，用物品作为 SourceObject 生成 Spec，并覆盖同槽位默认能力。
  - 当前实现对武器 `AbilityLevel <= 0` 没有在此处回退到角色等级。
- `AddSlottedGameplayAbilities()`：为期望槽位生成 Spec；对应句柄无效时调用 `GiveAbility` 并保存新句柄。
- `RemoveSlottedGameplayAbilities(bRemoveAll)`：
  - 非强制移除时先重新生成期望 Spec。
  - 当前句柄找不到 Spec、目标槽位不再存在、能力类变化或 SourceObject 变化时，调用 `ClearAbility`。
  - 无论是否找到旧 Spec，待移除槽位的句柄都重置为无效句柄。
- `OnItemSlotChanged()` 直接调用 `RefreshSlottedGameplayAbilities()`；刷新只在能力已经初始化时先移除失效槽位能力，再补授予新能力。
- `ActivateAbilitiesWithItemSlot()`：从 `SlottedAbilities` 找句柄，调用 `TryActivateAbility`；槽位或 ASC 无效时返回 `false`。
- `GetActiveAbilitiesWithItemSlot()`：找到槽位 Spec 后遍历其 Ability Instances，把实例转换为 `URPGGameplayAbility` 加入输出数组。
- `ActivateAbilitiesWithTags()`：调用 ASC 的 `TryActivateAbilitiesByTag`。
- `GetActiveAbilitiesWithTags()`：转发给项目 ASC 的同名函数。
- `GetCooldownRemainingForTag()`：查询拥有任一传入冷却标签的活动效果；存在多个结果时选择剩余时间最长的一项，输出其剩余时间和总持续时间。
- `HandleDamage()` 无条件调用 Blueprint `OnDamaged`。
- `HandleHealthChanged()` 和 `HandleManaChanged()` 只在能力初始化完成后调用 Blueprint `OnHealthChanged` 和 `OnManaChanged`。
- `HandleMoveSpeedChanged()` 先把 Character Movement 的 `MaxWalkSpeed` 更新为当前 `MoveSpeed`，初始化完成后再调用 Blueprint `OnMoveSpeedChanged`。
- `GetGenericTeamId()`：由 `APlayerController` 控制时返回队伍 `0`，否则返回队伍 `1`。

#### `URPGAbilitySystemComponent`

- `GetActiveAbilitiesWithTags()`：按“全部标签匹配”取得可激活 Spec，遍历每个 Spec 的 Ability Instances，并把转换后的 `URPGGameplayAbility*` 追加到输出数组。
- `GetDefaultAbilityLevel()`：Owner Actor 是 `ARPGCharacterBase` 时返回角色等级，否则返回 `1`。
- `GetAbilitySystemComponentFromActor(Actor, LookForComponent)`：调用 `UAbilitySystemGlobals` 的同名查找逻辑，再转换为 `URPGAbilitySystemComponent`。

### 7. 属性与伤害公式

#### `URPGAttributeSet`

- `Health=1`、`MaxHealth=1`、`Mana=0`、`MaxMana=0`、`AttackPower=1`、`DefensePower=1`、`MoveSpeed=1`、`Damage=0`。
- 每个属性通过 `ATTRIBUTE_ACCESSORS` 生成属性对象 Getter、数值 Getter、Setter 和 Init 函数。
- `Health`、`MaxHealth`、`Mana`、`MaxMana`、`AttackPower`、`DefensePower`、`MoveSpeed` 参与网络复制；`OnRep_Health`、`OnRep_MaxHealth`、`OnRep_Mana`、`OnRep_MaxMana`、`OnRep_AttackPower`、`OnRep_DefensePower`、`OnRep_MoveSpeed` 分别调用 `GAMEPLAYATTRIBUTE_REPNOTIFY` 同步 ASC 内部表示。
- `Damage` 不复制，是伤害执行计算写入后立即消费的临时属性。
- `AdjustAttributeForMaxChange()`：最大值发生实际变化且存在 ASC 时，按 `CurrentValue / CurrentMaxValue` 保持当前百分比；旧最大值小于等于 `0` 时，把新最大值作为增量应用到当前属性。
- `PreAttributeChange()`：`MaxHealth` 变化时按比例调整 `Health`；`MaxMana` 变化时按比例调整 `Mana`。
- `PostGameplayEffectExecute()`：
  - 从 Effect Context 取得原始来源 ASC，从捕获标签中取得来源标签。
  - 只有修改操作为 Additive 时，`DeltaValue` 才使用本次 Magnitude；其他操作的 Delta 为 `0`。
  - 从目标 AbilityActorInfo 取得目标 Actor、PlayerController 和 `ARPGCharacterBase`。
  - 修改 `Damage` 时，从来源 ASC 解析来源 Actor 和 Controller；没有 PlayerController 时尝试使用 Pawn Controller，再由 Controller Pawn 确定来源角色；Context 中存在 Effect Causer 时用它覆盖 Damage Causer。
  - 从 Context 复制 `HitResult`；读取并清零 `Damage`。
  - 临时伤害大于 `0` 时，把 `Health` 裁剪到 `[0, MaxHealth]`，再调用目标角色的 `HandleDamage` 和 `HandleHealthChanged(-Damage)`，同时传递 HitResult、来源标签、来源角色和 Damage Causer。
  - 直接修改 `Health` 时裁剪到 `[0, MaxHealth]`，再调用 `HandleHealthChanged(DeltaValue, SourceTags)`。
  - 修改 `Mana` 时裁剪到 `[0, MaxMana]`，再调用 `HandleManaChanged(DeltaValue, SourceTags)`。
  - 修改 `MoveSpeed` 时不在属性集中裁剪，直接调用 `HandleMoveSpeedChanged(DeltaValue, SourceTags)`。

#### `URPGDamageExecution`

- `RPGDamageStatics` 保存 `DefensePower`、`AttackPower`、`Damage` 三个属性捕获定义；`DamageStatics()` 返回进程内复用的静态实例。
- 构造时把 `DefensePower`、`AttackPower`、`Damage` 三个捕获定义加入 `RelevantAttributesToCapture`。
- `Execute_Implementation()` 执行以下捕获、计算和输出逻辑。
- `DefensePower` 从目标捕获且不快照；执行时读取目标当前值。
- `AttackPower` 和 `Damage` 从来源捕获并快照；创建 GameplayEffectSpec 时固定数值。
- 执行时把来源和目标的聚合标签放入 `FAggregatorEvaluateParameters`，用于计算带标签条件的属性修饰。
- 公式为 `DamageDone = Damage * AttackPower / DefensePower`；`DefensePower == 0` 时按 `1` 计算。
- 只有 `DamageDone > 0` 时，才向输出加入一个对目标 `Damage` 属性的 Additive Modifier。

### 8. 能力容器、目标收集与自定义任务

#### `FRPGGameplayEffectContainer`

- `TargetType`：`TSubclassOf<URPGTargetType>`，指定目标收集类。
- `TargetGameplayEffectClasses`：需要对目标生成 Spec 的 Gameplay Effect 类数组。
- 该结构保存静态配置，不保存运行时目标或 Spec。

#### `FRPGGameplayEffectContainerSpec`

- `TargetData`：运行时计算出的 `FGameplayAbilityTargetDataHandle`。
- `TargetGameplayEffectSpecs`：运行时生成的 `FGameplayEffectSpecHandle` 数组。
- `HasValidEffects()`：数组数量大于 `0` 时返回 `true`，不逐个检查 SpecHandle 是否有效。
- `HasValidTargets()`：`TargetData.Num() > 0` 时返回 `true`。
- `AddTargets()`：每个 `FHitResult` 分别创建一个 `FGameplayAbilityTargetData_SingleTargetHit`；Actor 数组非空时创建一个 `FGameplayAbilityTargetData_ActorArray` 并整体追加。

#### `URPGGameplayAbility`

- `EffectContainerMap`：`FGameplayTag -> FRPGGameplayEffectContainer`。
- `MakeEffectContainerSpecFromContainer(Container, EventData, OverrideGameplayLevel)`：
  - 取得 Owning Actor、对应的 `ARPGCharacterBase` 和项目 ASC；ASC 无效时返回空 Spec。
  - `TargetType` 有效时取得其 CDO，以 Owning Character、Avatar Actor 和 EventData 调用 `GetTargets`，再把返回的 HitResult 与 Actor 加入目标数据。
  - `OverrideGameplayLevel == INDEX_NONE` 时改用当前 Ability 实例的 `GetAbilityLevel()`。
  - 遍历 `TargetGameplayEffectClasses`，为每个类调用 `MakeOutgoingGameplayEffectSpec` 并加入结果数组。
- `MakeEffectContainerSpec(ContainerTag, EventData, OverrideGameplayLevel)`：从 Map 查找标签；找到时调用上述函数，找不到时返回空 Spec。
- `ApplyEffectContainerSpec(ContainerSpec)`：遍历全部 EffectSpecHandle，对同一份 TargetData 调用 `K2_ApplyGameplayEffectSpecToTarget`，合并返回的 ActiveEffectHandle。
- `ApplyEffectContainer()`：先按标签生成 Spec，再调用 `ApplyEffectContainerSpec()`。

#### `URPGTargetType`

- `URPGTargetType.GetTargets()` 是 BlueprintNativeEvent；`GetTargets_Implementation()` 的 C++ 默认实现不添加任何目标。
- `URPGTargetType_UseOwner.GetTargets_Implementation()` 把传入的 `TargetingCharacter` 加入 Actor 输出数组。
- `URPGTargetType_UseEventData.GetTargets_Implementation()` 优先读取 `EventData.ContextHandle.GetHitResult()` 并加入 HitResult 数组；没有 HitResult 但 `EventData.Target` 有效时，把 Target 加入 Actor 数组。

#### `URPGAbilityTask_PlayMontageAndWaitForEvent`

- 对外委托为 `OnCompleted`、`OnBlendOut`、`OnInterrupted`、`OnCancelled`、`EventReceived`；统一传递 `EventTag` 和 `FGameplayEventData`。
- 任务保存 `MontageToPlay`、`EventTags`、`Rate`、`StartSection`、`AnimRootMotionTranslationScale`、`bStopWhenAbilityEnds`；构造默认 `Rate=1`、`bStopWhenAbilityEnds=true`。
- `PlayMontageAndWaitForEvent()`：先应用非 Shipping 全局 Ability 播放速率缩放，创建任务并保存全部参数，不立即播放。
- `GetTargetASC()`：把任务持有的 `AbilitySystemComponent` 转换为 `URPGAbilitySystemComponent`。
- `Activate()`：
  - Ability 为空时直接返回。
  - 取得项目 ASC 和 AnimInstance，先注册 GameplayEvent 标签容器委托，再从指定 Section 播放 Montage。
  - 播放成功后绑定 Ability Cancelled、Montage BlendingOut 和 Montage End 委托。
  - 角色为 Authority，或为使用 LocalPredicted Ability 的 AutonomousProxy 时，设置 Root Motion Translation Scale。
  - ASC、AnimInstance 或 Montage 播放失败时记录警告并广播 `OnCancelled`。
  - 最后调用 `SetWaitingOnAvatar()`。
- `OnGameplayEvent()`：复制 Payload，把接收到的 EventTag 写入 `TempData.EventTag`，再广播 `EventReceived`。
- `OnMontageBlendingOut()`：当前 Ability 和 Montage 匹配时清除 AnimatingAbility，并在允许修改 Root Motion 的网络角色上把缩放恢复为 `1`；中断时广播 `OnInterrupted`，否则广播 `OnBlendOut`。
- `OnMontageEnded()`：非中断结束时广播 `OnCompleted`，随后调用 `EndTask()`。
- `OnAbilityCancelled()`：`StopPlayingMontage()` 成功时广播 `OnCancelled`。
- `ExternalCancel()`：ASC 有效时调用 `OnAbilityCancelled()`，随后无条件调用父类取消逻辑。
- `StopPlayingMontage()`：只在当前 ASC 的 AnimatingAbility 和 CurrentMontage 都与任务匹配时解绑 Montage 委托并停止 Montage。
- `OnDestroy(AbilityEnded)`：移除 Ability Cancelled 委托；Ability 已结束且 `bStopWhenAbilityEnds=true` 时停止 Montage；从 ASC 移除 GameplayEvent 委托，再调用父类实现。
- `GetDebugString()`：输出任务指定的 Montage 和 AnimInstance 当前播放的 Montage 名称。

### 9. 蓝图辅助、游戏模式、加载界面

#### `URPGBlueprintLibrary`

- `PlayLoadingScreen(bPlayUntilStopped, PlayTime)`：加载 `ActionRPGLoadingScreen` 模块并调用 `StartInGameLoadingScreen`。
- `StopLoadingScreen()`：加载同一模块并调用 `StopInGameLoadingScreen`。
- `IsInEditor()`：返回全局变量 `GIsEditor`。
- `EqualEqual_RPGItemSlot()`、`NotEqual_RPGItemSlot()`：调用结构体的相等和不等运算符。
- `IsValidItemSlot()`：调用 `FRPGItemSlot::IsValid()`；只检查类型和非负编号，不检查 GameInstance 配置的槽位上限。
- `DoesEffectContainerSpecHaveEffects()`、`DoesEffectContainerSpecHaveTargets()`：分别调用 Spec 的 `HasValidEffects()` 和 `HasValidTargets()`。
- `AddTargetsToEffectContainerSpec()`：复制传入 Spec，在副本上调用 `AddTargets()`，返回新 Spec，不修改原对象。
- `ApplyExternalEffectContainerSpec()`：只处理有效的 EffectSpecHandle；对每个 Spec 遍历 `TargetData.Data`，直接调用 TargetData 的 `ApplyGameplayEffectSpec`，合并全部活动效果句柄。
- `GetProjectVersion()`：从 `GGameIni` 的 `/Script/EngineSettings.GeneralProjectSettings` 读取 `ProjectVersion`。

#### `ARPGGameModeBase`

- 构造时把 `GameStateClass` 设为 `ARPGGameStateBase`，把 `PlayerControllerClass` 设为 `ARPGPlayerControllerBase`，并把 `bGameOver` 设为 `false`。
- `ResetLevel()`：不调用父类重置，直接触发 BlueprintImplementableEvent `K2_DoRestart`。
- `HasMatchEnded()`：返回 `bGameOver`。
- `GameOver()`：仅在 `bGameOver=false` 时调用 BlueprintImplementableEvent `K2_OnGameOver`，随后把 `bGameOver` 设为 `true`；后续调用不再触发事件。

#### `ARPGGameStateBase`

- 继承 `AGameStateBase`，构造函数为空，没有新增字段或执行逻辑。

#### `ActionRPGLoadingScreen`

- `IActionRPGLoadingScreenModule::Get()` 使用 `LoadModuleChecked` 加载并返回模块。
- `FRPGLoadingScreenBrush` 构造时同步加载传入纹理路径并设为 Slate Brush Resource；其 `AddReferencedObjects()` 会在资源存在时调用 `Collector.AddReferencedObject`，但当前结构声明没有继承 `FGCObject`。
- `SRPGLoadingScreen::Construct()`：
  - 加载固定资源 `/Game/UI/T_ActionRPG_TransparentLogo.T_ActionRPG_TransparentLogo`，Brush 尺寸为 `1024 x 256`。
  - 创建颜色 `(0.034, 0.034, 0.034, 1)` 的背景、中央 Logo 和右下角 Throbber。
  - MoviePlayer 报告加载完成时隐藏 Throbber，否则显示。
- `StartupModule()`：为 Cooker 引用强制加载 Logo；MoviePlayer 可用时调用 `CreateScreen()` 注册启动加载界面。
- `FActionRPGLoadingScreenModule` 实现 `IActionRPGLoadingScreenModule`，并由 `IMPLEMENT_GAME_MODULE` 注册。
- `IsGameModule()` 返回 `true`。
- `StartInGameLoadingScreen(bPlayUntilStopped, PlayTime)`：
  - `bAutoCompleteWhenLoadingCompletes = !bPlayUntilStopped`。
  - `bWaitForManualStop = bPlayUntilStopped`。
  - `bAllowEngineTick = bPlayUntilStopped`。
  - `MinimumLoadingScreenDisplayTime = PlayTime`。
  - 创建 `SRPGLoadingScreen` 并传给 `SetupLoadingScreen`。
- `StopInGameLoadingScreen()` 调用 `GetMoviePlayer()->StopMovie()`。
- `CreateScreen()`：注册自动随加载完成结束的启动界面，最短显示时间为 `3` 秒。

# 蓝图部分

## 能力蓝图

### Shared

- `GA_AbilityBase`
  - 所有能力蓝图的最底层父类。
  - 主要负责在结束时广播 `OnAbilityEnded`。
- `GE_StatsBase`
  - 玩家和敌人的基础属性模板。
  - 初始化 `MaxHealth`、`MaxMana`、`AttackPower`、`DefensePower`、`MoveSpeed`。
- `GE_DamageBase`
  - 所有伤害类 GE 的通用父类。
  - 使用 `RPGDamageExecution`。
  - 默认抓 `Damage`，读 `AttackDamage.DefaultAttack`，忽略 `Status.DamageImmune`。
- `GE_RangedBase`
  - 远程伤害模板。
  - 结构和 `GE_DamageBase` 一致，只是在资源语义上作为远程模板。
- `GE_HealBase`
  - 治疗模板。
  - 默认改 `Health`，按 `AttackDamage.HeavyAttack` 做数值来源。
- `GE_DamageImmune`
  - 无限持续。
  - 给目标打上 `Status.DamageImmune`。
- `GE_GodMode`
  - 表现上和 `GE_DamageImmune` 一样，也是常驻免伤。
- `BP_AbilityProjectileBase`
  - 通用能力投射物基类。
  - 负责飞行、重叠检测、命中去重、排除 Instigator、应用外部容器效果。
- `TargetType_SphereTrace`
  - 所有球形扫描目标类型的共用蓝图基底。
- `GA_MeleeBase`
  - 通用近战能力。
  - 通过 `Event.Montage.Shared.WeaponHit -> RPGTargetType_UseEventData + GE_MeleeBase` 处理命中。
  - 提交能力后播蒙太奇，收到事件后应用容器，结束时停止任务。
- `GE_MeleeBase`
  - 通用近战伤害模板。
  - 用 `AttackDamage.DefaultAttack`。
- `GA_PotionBase`
  - 通用药水能力。
  - 通过 `Event.Montage.Shared.UseItem -> RPGTargetType_UseOwner + GE_HealBase` 对自己生效。
  - 在效果应用后从库存扣掉当前物品。
- `GA_SkillBase`
  - 通用即时技能能力。
  - 通过 `Event.Montage.Shared.UseSkill -> GE_RangedBase` 生成默认远程效果容器。
  - 玩家具体技能一般额外挂 `GE_PlayerSkillManaCost` 和 `GE_PlayerSkillCooldown`。
- `GA_SpawnProjectileBase`
  - 通用投射物技能能力。
  - 通过 `Event.Montage.Shared.UseSkill` 创建 `GE_RangedBase` 容器，再把容器交给投射物。

### Player

#### 基础属性与通用资源

- `GE_PlayerStats`
  - 玩家开局属性模板。
  - 覆盖 `PlayerMaxHealth`、`PlayerMaxMana`、`PlayerAttackPower`、`PlayerDefensePower`、`PlayerMoveSpeed`。
- `GE_PlayerSkillManaCost`
  - 统一技能耗蓝，固定 `-10 Mana`。
- `GE_PlayerSkillCooldown`
  - 统一技能冷却，持续 `2 秒`，标签为 `Cooldown.Skill`。

#### 斧子近战分支

- `GA_PlayerAxeMelee`
  - 普通命中走 `GE_PlayerAxeMelee`。
  - `BurstPound` 走 `TargetType_BurstPound + GE_PlayerAxeBurstPound`。
  - `GroundPound` 走 `TargetType_GroundPound + GE_PlayerAxeGroundPound`。
- `GE_PlayerAxeMelee`
  - 普通斧击，使用 `AttackDamage.DefaultAttack`。
- `GE_PlayerAxeGroundPound`
  - 地面重击，使用 `AttackDamage.HeavyAttack`。
- `GE_PlayerAxeBurstPound`
  - 爆发重击，使用 `AttackDamage.HeavyAttack`。
- `TargetType_GroundPound`
  - 前方型范围：`Offset=200`、`TraceLength=600`、`Radius=100`。
- `TargetType_BurstPound`
  - 近身型范围：`Offset=0`、`TraceLength=300`、`Radius=100`。

#### 锤子近战分支

- `GA_PlayerHammerMelee`
  - 普通命中走 `GE_PlayerHammerMelee`。
  - `BurstPound` 走 `TargetType_HammerBurstPound + GE_PlayerHammerBurstPound`。
  - `GroundPound` 走 `TargetType_HammerGroundPound + GE_PlayerHammerGroundPound`。
- `GE_PlayerHammerMelee`
  - 普通锤击，使用 `AttackDamage_Hammer.DefaultAttack`。
- `GE_PlayerHammerGroundPound`
  - 锤子地面重击，使用 `AttackDamage_Hammer.HeavyAttack`。
- `GE_PlayerHammerBurstPound`
  - 锤子爆发重击，使用 `AttackDamage_Hammer.HeavyAttack`。
- `TargetType_HammerGroundPound`
  - 前方型范围：`Offset=200`、`TraceLength=600`、`Radius=150`。
  - 资源路径中保留 `HAmmer` 的大小写拼写现状。
- `TargetType_HammerBurstPound`
  - 近身型范围：`Offset=0`、`TraceLength=300`、`Radius=120`。

#### 剑近战分支

- `GA_PlayerSwordMelee`
  - 普通命中走 `GE_PlayerSwordMelee`。
  - `ChestKick` 走 `TargetType_ChestKick + GE_PlayerSwordChestKick`。
  - `FrontalAttack` 走 `TargetType_FrontalAttack + GE_PlayerSwordFrontalAttack`。
  - `JumpSlam` 走 `TargetType_JumpSlam + GE_PlayerSwordJumpSlam`。
- `GE_PlayerSwordMelee`
  - 普通剑击，使用 `AttackDamage_Sword.DefaultAttack`。
- `GE_PlayerSwordChestKick`
  - 踢击伤害，使用 `AttackDamage_Sword.MediumAttack`。
- `GE_PlayerSwordFrontalAttack`
  - 正前斩伤害，使用 `AttackDamage_Sword.MediumAttack`。
- `GE_PlayerSwordJumpSlam`
  - 跳劈伤害，使用 `AttackDamage_Sword.HeavyAttack`。
- `TargetType_ChestKick`
  - 近身宽范围：`Offset=150`、`TraceLength=100`、`Radius=200`。
- `TargetType_FrontalAttack`
  - 前方窄线型：`Offset=200`、`TraceLength=400`、`Radius=90`。
- `TargetType_JumpSlam`
  - 原地落地大范围：`Offset=0`、`TraceLength=1`、`Radius=300`。

#### 武器拾取能力

- `GA_WeaponPickUp`
  - 拾取武器并挂入槽位后，授予对应的剑系近战能力。
  - 普通命中走 `GE_WeaponPickUp`。
  - 三个连段分支直接复用玩家剑系的目标类型和伤害 GE。
- `GE_WeaponPickUp`
  - 继承 `GE_MeleeBase`。
  - 使用 `AttackDamage_Sword.DefaultAttack`。
  - `ScalableFloatMagnitude = 5`。
  - 没有额外配置 `Status.DamageImmune` 忽略标签。

#### 药水与增益

- `GA_PotionHealth`
  - 用药事件给自己套 `GE_PotionHealth`。
- `GE_PotionHealth`
  - 直接回生命。
  - 读取 `StartingStats.PlayerMaxHealth`。
- `GA_PotionMana`
  - 用药事件给自己套 `GE_PotionMana`。
- `GE_PotionMana`
  - 回蓝。
  - 读取 `StartingStats.PlayerMaxMana`，倍率 `0.5`。
- `GA_PotionPowerBoost`
  - 用药事件给自己套 `GE_Powerboost`。
- `GE_Powerboost`
  - 持续 `6 秒`。
  - 修改 `AttackPower`。
  - 使用 `CT_PowerBoost.HeavyAttack`，倍率约 `2.3`。
  - 触发 `GameplayCue.Potion.PowerBoost`。
- `GC_PowerBoost`
  - `On Burst` 开启强化表现。
  - `On Cease Relevant` 停掉表现。
- `GA_DeathsDoor`
  - 用药事件给自己套 `GE_DeathsDoor`。
- `GE_DeathsDoor`
  - 同时回半血半蓝。

#### 技能分支

- `GA_PlayerSkillFireball`
  - 投射物技能。
  - 播放 `AM_Skill_Fireball`。
  - 生成 `BP_Fireball`。
  - 施法附带统一耗蓝和冷却。
  - 命中规格为 `GE_PlayerSkillFireball`。
- `BP_Fireball`
  - 复用通用投射物逻辑。
  - 增加火球粒子、音效、点光源和命中特效。
- `GE_PlayerSkillFireball`
  - 使用 `AttackDamage.HeavyAttack`。
  - 额外倍率 `1.5`。

- `GA_PlayerSkillFireWave`
  - 即时前方范围技能。
  - `Event.Montage.Shared.UseSkill -> TargetType_FireWave + GE_PlayerSkillFireWave`。
- `GE_PlayerSkillFireWave`
  - 使用 `AttackDamage.HeavyAttack`。
- `TargetType_FireWave`
  - 前方线状范围：`Offset=200`、`TraceLength=600`、`Radius=100`。

- `GA_PlayerSkillMeteor`
  - 近身陨石范围技能。
  - `Event.Montage.Shared.UseSkill -> TargetType_Meteor + GE_PlayerSkillMeteor`。
- `GE_PlayerSkillMeteor`
  - 使用 `AttackDamage.MediumAttack`。
- `TargetType_Meteor`
  - 原地圆形范围：`Offset=0`、`TraceLength=1`、`Radius=300`。

- `GA_PlayerSkillMeteorStorm`
  - 大范围清场技能。
  - `Event.Montage.Shared.UseSkill -> TargetType_MeteorStorm + GE_PlayerSkillMeteorStorm`。
- `GE_PlayerSkillMeteorStorm`
  - 使用 `AttackDamage.HeavyAttack`。
- `TargetType_MeteorStorm`
  - 超大圆形范围：`Offset=0`、`TraceLength=1`、`Radius=600`。

- `BP_SlimeBall`
  - 通用可复用投射物表现。
  - 后面也被哥布林远程能力复用。

### Goblin

- `GE_GoblinStats`
  - 使用默认敌人属性表，整体就是标准敌人数值模板。
- `GA_GoblinMelee`
  - 复用 `GA_MeleeBase`。
  - 普通命中走 `GE_GoblinMelee`。
- `GE_GoblinMelee`
  - 使用 `AttackDamage.DefaultAttack`。
- `GA_GoblinRange01`
  - 远程投射物技能。
  - 发射 `BP_SlimeBall`。
  - 命中伤害直接复用 `GE_PlayerSkillFireball`。
  - 冷却 GE 使用 `GE_GoblinRange`。
- `GE_GoblinRange`
  - 表面上是冷却 GE，实际同时还保留了伤害执行项。
  - 持续 `2 秒`，标签是 `Cooldown.Skill`。
  - 还包含 `1.5` 倍的普通攻击伤害执行。

### Spider

- `GE_SpiderStats`
  - 更硬、更疼、更快。
  - `MaxHealth` 按默认值 `10` 倍规模写入。
  - `AttackPower` 为默认 `2` 倍。
  - `MoveSpeed` 为默认 `1.5` 倍。
- `GA_SpiderMelee`
  - 近身近战。
  - 标签 `Ability.Melee.Close`。
  - 直接复用 `GE_MeleeBase`。
- `GA_SpiderCharge`
  - 冲撞近战。
  - 标签 `Ability.Melee.Far`。
  - 命中走 `GE_SpiderCharge`。
- `GE_SpiderCharge`
  - 重冲锋伤害。
  - 读取目标当前 `DefensePower`。
  - 使用 `AttackDamage.HeavyAttack`，倍率 `2`。
- `GA_SpiderFirewall`
  - 前方大范围火墙技能。
  - `Event.Montage.Shared.UseSkill -> TargetType_Firewall + GE_FirewaveDamage`。
- `TargetType_Firewall`
  - 超长前方范围：`Offset=500`、`TraceLength=1000`、`Radius=300`。
- `GE_FirewaveDamage`
  - 使用 `AttackDamage.HeavyAttack`。
  - 忽略 `Status.DamageImmune`。
  - 附带一个只校验 `Ability.Skill` 但未配置附加 EffectClass 的条件效果项。

## 角色与运行蓝图

### 1. `BP_Character`

#### `DoMeleeAttack`

先检查 `IsUsingMelee` 和 `CanUseAnyAbility`。角色没有正在执行近战能力且允许使用能力时，从 `SlottedAbilities` 中查找传入的 `RPGItemSlot`；找到对应的 `FGameplayAbilitySpecHandle` 后调用能力激活函数，并返回激活结果。

![BP_Character DoMeleeAttack](Screenshots/BP_Character_DoMeleeAttack.png)

![image-20260709104336120](Screenshots/image-20260709104336120.png)

#### `IsUsingMelee`

使用 `Ability.Melee` 标签调用 `GetActiveAbilitiesWithTags`，取得当前激活的近战能力实例数组；数组长度大于 `0` 时返回 `true`。

![image-20260709111017305](Screenshots/image-20260709111017305.png)

#### `CanUseAnyAbility`

依次检查角色存活、游戏没有暂停、当前没有使用技能；三个条件同时满足时返回 `true`。

![image-20260709110404504](Screenshots/image-20260709110404504.png)

#### `IsAlive`

读取角色 `Health`，当 `Health > 0` 时返回 `true`。

![image-20260709110835692](Screenshots/image-20260709110835692.png)

#### `IsUsingSkill`

使用 `Ability.Skill` 标签调用 `GetActiveAbilitiesWithTags`，取得当前激活的技能能力实例数组；数组长度大于 `0` 时返回 `true`。

![image-20260709113145085](Screenshots/image-20260709113145085.png)

#### `DoSkillAttack`

`CanUseAnyAbility` 返回 `true` 时，通过 `Ability.Skill` 标签激活能力，并广播 `Call On Skill Attack`。

![image-20260709112337147](Screenshots/image-20260709112337147.png)

#### `UseItemPotion`

创建 `RPGItemSlot`，把类型设为 `Potion`、槽位索引设为 `0`，再用该槽位激活对应能力。

![image-20260709113512891](Screenshots/image-20260709113512891.png)

### 2. `BP_EnemyCharacter`

#### `Event OnHealthChanged`

属性系统触发生命变化事件后，把 `Delta Value` 作为 `Damage` 参数传给 `ShowDamageAmount`，然后调用 `ShowHealthBar`。接着对 `IsAlive` 取反；角色已经不存活时，通过 `Do Once` 调用 `OnDeathEvent`，保证死亡流程只执行一次。

![image-20260709114537441](Screenshots/image-20260709114537441.png)

#### `ShowDamageAmount`

读取敌人当前 `Actor Transform`，以该 Transform 调用 `Add WC Damage Text` 创建伤害文字组件，使用手动附着；再把返回的 `WC Damage Text` 组件和传入的 `Damage` 交给 `Set Damage Text`，显示本次生命变化数值。

![image-20260709114636085](Screenshots/image-20260709114636085.png)

#### `ShowHealthBar`

取得 `HPWidget` 组件并转换为 `WB_EnemyHP`，把 `Health / MaxHealth` 写入血条百分比。显示或隐藏分支经过 `Do Once`，只执行一次可见性设置；当前生命百分比满足 `HPBarShowPercent` 的判断条件时显示血条，否则隐藏。

![BP_EnemyCharacter ShowHealthBar](Screenshots/BP_EnemyCharacter_ShowHealthBar.png)

#### `OnDeathEvent`

调用 `IncreaseNPCKillCount`；取得 `HPWidget` 并隐藏敌人血条；从控制器解除当前 Pawn；修改敌人碰撞对象类型；如果 `CurrentWeapon` 有效则销毁武器。

![BP_EnemyCharacter OnDeathEvent](Screenshots/BP_EnemyCharacter_OnDeathEvent.png)

#### `Event Destroyed`

敌人 Actor 被销毁时通过 `Do Once` 执行掉落流程：调用 `Spawn Loot`，随后调用父类的 `Destroyed`。

![image-20260710104907834](Screenshots/image-20260710104907834.png)

#### `Spawn Loot`

按蓝图配置的随机权重选择掉落类型，并在敌人位置生成武器、药水或魂拾取物。

![image-20260709160944146](Screenshots/image-20260709160944146.png)

### 3. `BP_PlayerCharacter`

#### 输入事件

`IA_NormalAttack` 调用 `DoMeleeAttack`；`IA_SpecialAttack` 调用特殊技能入口；`IA_UseItem` 激活药水能力；`IA_Roll` 调用 `DoRoll`；`IA_Run` 修改 Character Movement 的移动速度；`IA_ChangeWeapon` 调用 `SwitchWeapon`。

![image-20260709161529426](Screenshots/image-20260709161529426.png)

#### `DoMeleeAttack`

先调用 `CanUseAnyAbility`，返回 `false` 时直接结束。允许使用能力时检查 `IsUsingMelee`：正在近战能力中则调用 `JumpSectionForCombo` 并返回 `false`；没有正在近战能力中则用 `CurrentWeaponSlot` 调用 `ActivateAbilitiesWithItemSlot`，把激活结果作为返回值。

![BP_PlayerCharacter DoMeleeAttack](Screenshots/BP_PlayerCharacter_DoMeleeAttack.png)

#### `JumpSectionForCombo`

从 Mesh 取得 Anim Instance，并检查 `JumpSectionNotify` 引用有效。`bEnableComboPeriod` 为 `true` 时，读取当前活动 Montage 和当前 Section；从 `JumpSectionNotify.JumpSections` 中随机选择一个 Section，调用 `Montage Set Next Section` 把当前 Section 的下一段改为选中 Section，随后把 `bEnableComboPeriod` 设为 `false`。

![image-20260709175449821](Screenshots/image-20260709175449821.png)

#### `UseItemPotion`

创建 `RPGItemSlot`，把物品类型设为 `Potion`、槽位索引设为 `0`，再用该结构体调用槽位能力激活函数。

![image-20260710165430689](Screenshots/image-20260710165430689.png)

#### `DoRoll`

`CanUseAnyAbility` 返回 `true` 时，把 `Rolling` Montage 传给 `PlayHighPriorityMontage` 播放。

![image-20260710165817565](Screenshots/image-20260710165817565.png)

#### `PlayHighPriorityMontage`

从 Mesh 取得 Anim Instance；当前没有 Montage 正在播放时，保存传入的高优先级 Montage，然后使用传入的 Section 名播放该 Montage。

![image-20260710165921423](Screenshots/image-20260710165921423.png)

#### `SwitchWeapon`

读取当前活动 Montage；没有有效 Montage 时调用 `AttachNextWeapon`。

![image-20260710170338547](Screenshots/image-20260710170338547.png)

#### `CreateAllWeapons`

先销毁 `EquippedWeapons` 中已有的全部武器 Actor 并清空数组；读取所有 Weapon 类型的已装备 Item 存入 `EquippedRPGItems`。遍历这些 Item，转换为 `RPGWeaponItem`，读取其中的 `WeaponActor` 类，在指定 Transform 生成 Actor，并加入 `EquippedWeapons`。

遍历结束后检查 `FinishAttack`：为 `true` 时调用 `AttachNextWeapon` 并把它重置为 `false`；否则检查 `CurrentWeaponIndex + 1` 对应武器是否有效，有效时调用 `WeaponAttachMethod`，无效时调用 `AttachNextWeapon`。

![image-20260713103254729](Screenshots/image-20260713103254729.png)

#### `AttachNextWeapon`

用 `EquippedWeapons.Length - 1` 得到最后一个有效索引。若 `CurrentWeaponIndex + 1` 超过最后索引，则把当前索引设为 `0`；否则索引加 `1`。随后调用 `WeaponAttachMethod` 挂接该索引对应的武器，并返回新的当前武器索引。

![image-20260710170448890](Screenshots/image-20260710170448890.png)

#### `WeaponAttachMethod`

如果 `CurrentWeapon` 有效，先把它从角色上卸下。然后从 `EquippedWeapons` 取得传入索引对应的武器 Actor，保存为 `CurrentWeapon`；读取该武器对应的 `RPGItemSlot` 保存为 `CurrentWeaponSlot`；把武器附着到角色 Mesh 的 `hand_rSocket`；最后通过玩家控制器触发装备图标更新。

![BP_PlayerCharacter WeaponAttachMethod](Screenshots/BP_PlayerCharacter_WeaponAttachMethod.png)

![image-20260710171639721](Screenshots/image-20260710171639721.png)

![image-20260713110034836](Screenshots/image-20260713110034836.png)

#### `ActivateInventoryCamera`

传入 `true` 时保存库存相机状态，显示库存灯光，激活库存摄像机并停用第三人称摄像机；允许角色 Mesh 在暂停时 Tick；保存角色原 Transform；查找带 `InventoryPosition` 标签的 Actor，并把角色移动到其 Transform。

传入 `false` 时隐藏库存灯光，停用库存摄像机并重新激活第三人称摄像机；关闭 Mesh 的暂停 Tick；把角色恢复到先前保存的 Transform。

![image-20260710142603475](Screenshots/image-20260710142603475.png)

#### `Event BeginPlay`

设置 Actor 在游戏暂停时仍执行 Tick；取得玩家摄像机，并执行持续 `1` 秒、数值从 `1` 到 `0` 的 Camera Fade。

![image-20260709164903477](Screenshots/image-20260709164903477.png)

#### `Event OnManaChanged`

法力属性变化时调用 `UpdateManaBar`。

![image-20260709162011425](Screenshots/image-20260709162011425.png)

#### `UpdateManaBar`

取得 `BP_PlayerController` 以及控制器保存的 HUD 引用，把 `Mana / MaxMana` 传给 HUD 的法力条更新入口。UI 内部不展开。

![image-20260709162129572](Screenshots/image-20260709162129572.png)

#### `Event OnHealthChanged`

先调用 `UpdateHealthBar`。随后对 `IsAlive` 取反；玩家不存活时，从 `BP_GameMode` 调用 `GameOver`，再调用 `DebugFinish`。

![image-20260709165126946](Screenshots/image-20260709165126946.png)

#### `DebugFinish`

检查玩家是否存活；不存活时停止调试流程。

![image-20260709172617866](Screenshots/image-20260709172617866.png)

#### `Event OnDamaged`

受到伤害时先调用 `DebugUpdate`。检查当前是否受护盾保护；未受保护时，从受击 Montage 的起始 Section `0` 和 `1` 中随机选择一个播放，并调用 `PlayMaterialEffect` 写入受击材质参数。

![image-20260709173746856](Screenshots/image-20260709173746856.png)

#### `DebugUpdate`

调试流程给玩家应用无敌 Gameplay Effect；玩家死亡时停止调试；并从能力来源移除调试流程激活的 Gameplay Effect。

![image-20260709173137585](Screenshots/image-20260709173137585.png)

#### `PlayMaterialEffect`

接收 `LinearColor` 和材质参数名，把颜色写入 Mesh 材质参数，并按传入时间更新对应材质效果数值。

![image-20260709174522147](Screenshots/image-20260709174522147.png)

### 4. `BP_PlayerController`

#### `BeginPlay`

把项目的 Enhanced Input Mapping Context 添加到本地玩家输入子系统。

![image-20260710112014769](Screenshots/image-20260710112014769.png)

#### `IA_Pause`

输入触发后调用 `BP_GameInstance` 的暂停入口，切换游戏暂停状态。

![image-20260714114108498](Screenshots/image-20260714114108498.png)

#### `IA_Inventory`

先确认没有处于自动战斗，再检查当前没有播放关卡序列、暂停界面没有打开且玩家角色存活；条件满足时调用 `ShowInventoryUI`。

![image-20260714114153105](Screenshots/image-20260714114153105.png)

#### `ShowInventoryUI`

检查玩家角色是否存活以及库存当前是否打开。

库存已打开且 `InventoryUI` 有效时：移除库存控件；延迟 `0.75` 秒后解除暂停；调用 `ActivateInventoryCamera(false)`；清空 `InventoryUI` 引用；恢复屏幕控制层；隐藏鼠标；把库存状态设为关闭；恢复玩家输入并设置 `Input Mode Game Only`。

库存未打开时：创建装备界面并保存引用，添加到视口；调用 `ActivateInventoryCamera(true)`；隐藏屏幕控制层；显示鼠标；把库存状态设为打开；禁用玩家角色输入并切换到界面输入模式。UI 控件内部不展开。

![BP_PlayerController ShowInventoryUI](Screenshots/BP_PlayerController_ShowInventoryUI.png)

#### `IA_AutoPlay`

Enhanced Input 的 `IA_Autoplay` 在 `Triggered` 时调用 `IsPlayingSequence` 并对结果取反。当前没有播放关卡序列时，通过 `Get Game Mode BP` 取得 `BP_GameMode`，调用其 `ToggleAutoBattleMode`；再把函数返回的 `Mode Result` 传给 `OnScreenControls.Set Autoplay Enabled`，同步自动战斗显示状态。正在播放关卡序列时不执行后续逻辑。

![BP_PlayerController IA_Autoplay](Screenshots/BP_PlayerController_AutoBattle.png)

#### `PlaySkippableCutscene`

保存传入的 Level Sequence，创建 Sequence Player 并开始播放，把 `On Finished` 绑定到 `StopPlayingSkippableCutscene`。随后创建过场提示控件并添加到视口，调用 `ShowHUD(false)` 隐藏 HUD，并广播过场开始事件。

正常播放结束和玩家按 Enter 跳过都进入 `StopPlayingSkippableCutscene`；该入口经 `Do Once` 停止当前 Sequence，调用 `ShowHUD(true)` 恢复 HUD，再调用 `StartGame`。

![image-20260710150323106](Screenshots/image-20260710150323106.png)

#### `ShowHUD`

根据传入的 `Visible` 和 `IsRunningOnMobile` 结果切换虚拟摇杆；`OnScreenControls` 有效时按 `Visible` 设置其可见性。UI 内部不展开。

![image-20260710151201253](Screenshots/image-20260710151201253.png)

#### `IsRunningOnMobile`

读取当前平台名称，根据平台名判断是否为移动端并返回布尔值。

![image-20260710153757728](Screenshots/image-20260710153757728.png)

#### `OnPossess`

经 `Do Once` 把新 Possess 的 Pawn 转换成 `BP_PlayerCharacter` 并保存；调用 `CreateHUD`；绑定库存物品变化监听到 `HandleInventoryItemChanged`；延迟 `0.025` 秒调用玩家角色的 `CreateAllWeapons`。

![image-20260713100950457](Screenshots/image-20260713100950457.png)

#### `CreateHUD`

`OnScreenControls` 已存在时直接调用 `ShowHUD`。不存在时根据 `IsRunningOnMobile` 选择移动端或 PC 端 HUD 类，创建后保存引用并添加到视口，调用图标更新，再调用 `ShowHUD`。UI 内部不展开。

![image-20260713101128242](Screenshots/image-20260713101128242.png)

#### `HandleInventoryItemChanged`

比较发生变化的 Item 与 `SoulsItem`；相同时从传入的 `FRPGItemData` 读取数量，并把该数量广播给魂数量显示入口。

![image-20260713102544658](Screenshots/image-20260713102544658.png)

#### `Tick`

仅在未开启自动战斗、`ControlledPawn` 有效且 `bBlockedMovement=false` 时处理移动。取得受控角色和其 Anim Instance，检查是否有 Montage 正在播放。

有 Montage 播放时不添加移动输入，只读取 `MoveDirectionXY` 和摄像机旋转计算目标朝向；输入方向长度大于 `0` 时更新角色旋转。没有 Montage 播放时，用摄像机的 Forward/Right Vector 和 `MoveDirectionXY` 调用 `AddMovementInput`，实现前后左右移动。

![image-20260713144317364](Screenshots/image-20260713144317364.png)

#### `MoveDirection`

输入事件把二维移动值保存到 `MoveDirectionXY`，由 Tick 消费。

![image-20260713145602030](Screenshots/image-20260713145602030.png)

#### `InputTouch`

`Pressed` 时把触点 `Location X` 保存到 `XPos` 并设置 `CanRotate=true`；`Released` 时设置 `CanRotate=false`；`Moved` 且允许旋转时，用当前触点 X 减去 `XPos`，乘 `0.25` 后加到当前 Control Rotation 的 Yaw，再写回控制器旋转。

![image-20260713150504701](Screenshots/image-20260713150504701.png)

#### `IA_Look`

把二维视角输入分别传给控制器的 Yaw 输入和 Pitch 输入。

![image-20260713151027272](Screenshots/image-20260713151027272.png)

### 5. `BP_GameMode`

#### `BeginPlay`

调用 `PlayDefaultIntroCutscene` 决定播放开场序列或直接开始游戏，并调用 `Play Sound 2D` 播放关卡音乐。

![BP_GameMode BeginPlay](Screenshots/BP_GameMode_BeginPlay.png)

#### `PlayDefaultIntroCutscene`

查找关卡内全部 Level Sequence Actor，检查索引 `0` 是否有效；有效时把该序列交给 `BP_PlayerController.PlaySkippableCutscene`，无效时直接调用 `StartGame`。

![image-20260714114451123](Screenshots/image-20260714114451123.png)

#### `StartGame`

检查 `GameInProgress`；为 `false` 时将其设为 `true`，调用 `RestartPlayer` 重新生成或接管玩家，然后调用 `StartPlayTimer` 和 `StartEnemySpawn`。

![image-20260710151627200](Screenshots/image-20260710151627200.png)

#### `StartPlayTimer`

创建每 `1` 秒触发一次的循环计时器并记录 `StartTime`。每次计时器触发都把 `BattleTime` 减 `1`，把剩余时间发送给 HUD 更新入口；`BattleTime <= 0` 时调用 `GameOver`。

![image-20260710152245735](Screenshots/image-20260710152245735.png)

#### `StartEnemySpawn`

调用 `GetRandomSpawnPoint` 检查场景中是否存在有效刷怪点；存在时延迟 `1` 秒调用 `StartNewWave`。

![image-20260710152327940](Screenshots/image-20260710152327940.png)

#### `GetRandomSpawnPoint`

取得场景中全部刷怪点。数组为空时返回 `Valid=false` 和默认 Transform；不为空时在 `0` 到最后索引之间随机选择一个刷怪点，返回其 Transform，并把 `Valid` 设为 `true`。

![image-20260710155050744](Screenshots/image-20260710155050744.png)

#### `StartNewWave`

用 `Wave_CurrentWave` 拼出 DataTable 行名并读取本波配置；拆出敌人组数组和波次时间并保存，然后调用 `SpawnNewWave`。

![image-20260710152707848](Screenshots/image-20260710152707848.png)

#### `SpawnNewWave`

创建波次开始提示并添加到视口；延迟 `3` 秒调用 `SpawnNextEnemiesGroup`，同时把当前 `BattleTime` 发送到显示入口。UI 内部不展开。

![image-20260710152804175](Screenshots/image-20260710152804175.png)

#### `SpawnNextEnemiesGroup`

从保存的 Wave 数组取索引 `0` 的敌人组，遍历组内的敌人类，并逐个调用 `SpawnEnemy`。

![image-20260710152839671](Screenshots/image-20260710152839671.png)

#### `SpawnEnemy`

先取得随机刷怪点，再用传入的敌人类在该 Transform 生成敌人。生成成功后把 `SpawnedEnemies` 加 `1`，把敌人的 `OnDestroyed` 绑定到本地销毁事件；敌人销毁时把计数减 `1`，然后调用 `CheckCurrentWave`。

![image-20260710152914294](Screenshots/image-20260710152914294.png)

#### `CheckCurrentWave`

检查 `SpawnedEnemies` 是否已经为 `0`。场上敌人清空后，若 Wave 数组索引 `0` 仍有效，则继续调用 `SpawnNextEnemiesGroup`；没有剩余敌人组时把 `CurrentWave` 加 `1`，延迟 `2` 秒调用 `OnWaveFinished`。

![image-20260710152944270](Screenshots/image-20260710152944270.png)

#### `OnWaveFinished`

创建波次结束提示并添加到视口，延迟 `4` 秒调用 `StartNewWave`。UI 内部不展开。

![image-20260710153015567](Screenshots/image-20260710153015567.png)

#### `IncreaseNPCKillCount`

每次调用给 `BattleTime` 增加 `5` 秒；通过 `GetPlayerControllerBP` 取得控制器以及其中保存的 HUD 引用，广播剩余时间刷新和时间奖励反馈；最后把击杀计数加 `1`。

![image-20260709152713072](Screenshots/image-20260709152713072.png)

#### `GetPlayerControllerBP`

先读取已保存的玩家控制器并检查有效性；有效时直接返回。无效时从 World 获取索引 `0` 的玩家控制器，转换为 `BP_PlayerController` 后返回。

![image-20260709153537015](Screenshots/image-20260709153537015.png)

#### `ToggleAutoBattleMode`

自动战斗已开启时，停止 AI Controller 的逻辑，把自动战斗状态设为 `false`，再让玩家控制器重新 Possess 玩家角色。自动战斗未开启时，调用 `InitializeAutoBattle`，把状态设为 `true`。最后返回当前自动战斗状态。

![image-20260710135427470](Screenshots/image-20260710135427470.png)

#### `InitializeAutoBattle`

取得并保存玩家控制器和玩家角色。已有自动战斗 AI Controller 时重启其逻辑，并让它 Possess 玩家角色；没有时生成 `acPlayer` Controller 和 `BP_SpectatorPawn`，保存两个引用，再让 AI Controller Possess 玩家角色。

![image-20260710140018779](Screenshots/image-20260710140018779.png)

#### `OnGameOver`

先把全局时间膨胀设为 `0.25`，延迟 `0.5` 秒后恢复为 `1`，随后暂停游戏。结算 UI 引用有效时直接添加到视口；无效时先创建 `InGameFinish` 控件并保存，再添加到视口；最后设置 `Input Mode UI Only`。UI 内部不展开。

![BP_GameMode OnGameOver](Screenshots/BP_GameMode_OnGameOver.png)

#### `Restart`

取得 `BP_GameInstance`，调用其 `RestartGameLevel`。

![image-20260714142912714](Screenshots/image-20260714142912714.png)

### 6. 运行框架蓝图

- `BP_GameInstance`
  - 继承 `RPGGameInstanceBase`。
  - 负责跨关卡流程、全局选项、存档入口、商店物品加载等。

#### `BP_GameInstance.RestartGameLevel`

启动加载界面，然后重新加载当前游戏关卡。

![image-20260714142851553](Screenshots/image-20260714142851553.png)

#### `BP_GameInstance.FadeInAndShowLoadingScreen`

执行画面淡入并调用项目加载界面播放入口。

![image-20260714165010306](Screenshots/image-20260714165010306.png)

#### `BP_GameInstance.LoadGameLevel`

移除当前全部 Widget，启动加载界面，再按传入的关卡名调用 `Open Level`。

![image-20260714165357947](Screenshots/image-20260714165357947.png)

- `BP_RPGFunctionLibrary`
  - 主要是从当前世界快速拿到项目具体类实例。
- `GlobalOptionsSaveGame`
  - 只存全局设置。

### 7. 敌人与战斗实体蓝图

- `NPC_GoblinBP`
  - BeginPlay 生成并挂接武器。
  - 受击时朝向命中来源并播受击表现。
- `NPC_Goblin_Level_01`
  - 近战哥布林外观与武器配置。
- `NPC_Goblin_Level_02`
  - 远程哥布林，配置 `GA_GoblinRange01`。
- `NPC_Goblin_Level_03`
  - 火把哥布林，死亡时有额外火焰表现。
- `NPC_SpiderBoss`
  - 生成蜘蛛武器。
  - 受击与死亡时追加蜘蛛自己的表现。
- `NPC_SpawnBox`
  - 只作为刷怪区域标记。

### 8. 武器与拾取物

- `WeaponActor`
  - 所有近战武器的共同父类。
  - 开关碰撞攻击窗口。
  - 重叠命中时构造 `GameplayEventData` 发回 Instigator。
  - 支持命中停顿和消耗型武器。
- `BP_WeaponSpider`
  - 命中时附加蜘蛛特效。
- `BP_Weapon_HellHammer`
  - 额外带点光源。
- `BP_Weapon_Sword_BlackKnight`
- `BP_Weapon_Sword_Talon`
- `BP_Weapon_Hammer_1`
- `BP_Weapon_Hammer_3`
- `FireAxeActor`
- `GreateBladeActor`
- `GuardianWeaponActor`
- `GoblinWeapon_Base`
- `GoblinWeapon_Axe`
- `GoblinWeapon_Torch`
  - 这些都主要复用 `WeaponActor` 的攻击窗口和命中事件逻辑，差别更多在资源配置。

- `BP_SoulItem`
  - 敌人掉落的魂。
  - 延迟后会吸向玩家。
  - 到达玩家后给控制器加资源并销毁。

#### `BP_RPGItem_Pickup_Base`

- 继承 `Actor`，包含 `ItemMesh`、`CollectCollision`、`Capsule` 和 `PointLight`。
- `ItemType` 保存拾取后加入库存的 `URPGItem` 资产，`Count` 保存本次给予数量，拾取颜色控制 Mesh/灯光表现。
- `ItemSetup` 读取 `ItemType` 的名称和拾取颜色，初始化拾取物显示。
- `CollectCollision` 重叠时检查目标是否带玩家标签；满足条件后添加 Impulse，调用 `GiveItem`，使用 `ItemType` 和 `Count` 调用玩家库存添加逻辑，再销毁拾取物。
- `Animate` 更新拾取物缩放和上下浮动。
- `AnimateCollection` 按 Impulse 把拾取物移向玩家；距离满足收集条件时结束收集。

#### `BP_Pickup_Health`

- 继承 `BP_RPGItem_Pickup_Base`，复用基类的碰撞、浮动、吸附、入库和销毁逻辑。
- `ItemType` 引用 `Content/Items/Potions/Potion_Health.uasset`。
- `Count = 1`。
- 拾取颜色设为绿色。

#### `BP_Pickup_Mana`

- 继承 `BP_RPGItem_Pickup_Base`。
- `ItemType` 引用 `Content/Items/Potions/Potion_Mana.uasset`。
- `Count = 1`。
- 拾取颜色设为蓝色。

#### `BP_Pickup_ManaHealth`

- 继承 `BP_RPGItem_Pickup_Base`。
- `ItemType` 引用 `Content/Items/Potions/Potion_DeathsDoor.uasset`。
- `Count = 1`。
- 拾取颜色设为红色；对应物品能力同时处理生命和法力恢复。

#### `BP_Weapon_Pickup`

- 继承 `BP_RPGItem_Pickup_Base`。
- `ItemType` 引用 `Content/Items/Pickups/WeaponPickups/Assets/Weapon_PickUp.uasset`。
- `Count = 10`。
- `GiveItem` 在物品加入库存后创建 Weapon 类型的 `RPGItemSlot`，设置当前装备槽位，并调用玩家角色的 `AttachNextWeapon` 更新手持武器。
- 该逻辑遍历已装备物品并按槽位索引寻找刚拾取的武器，使库存变化同步到当前武器 Actor。
