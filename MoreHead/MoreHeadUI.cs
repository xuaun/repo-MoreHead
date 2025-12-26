using System;
using BepInEx.Logging;
using MenuLib;
using UnityEngine;
using System.Reflection;
using HarmonyLib;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.IO;
using MenuLib.MonoBehaviors;
using MenuLib.Structs;
using BepInEx.Configuration;
using Steamworks;

namespace MoreHead
{
    public static class DecorationSortingExtension
    {
        public static IEnumerable<DecorationInfo> ApplySorting(this IEnumerable<DecorationInfo> decorations)
        {
            var sorted = decorations
                .OrderByDescending(d => d.IsVisible)
                .ThenByDescending(d => FavoritesManager.IsFavorite(d.DisplayName ?? ""))
                .ThenByDescending(d => NewItemsManager.IsNew(d.DisplayName ?? ""))
                .ThenBy(d => d.Name != d.DisplayName ? 1 : 0)
                .ThenBy(d => d.DisplayName)
                .ToList();

            return sorted;
        }
    }

    // MoreHead的UI管理类
    public static class MoreHeadUI
    {
        // 日志记录器
        private static ManualLogSource? Logger => Morehead.Logger;

        // 黑名单模式配置
        private static ConfigEntry<string>? _blacklistModeKey;

        // UI元素
        public static REPOPopupPage? decorationsPage;

        // 装饰物按钮字典
        public static Dictionary<string?, REPOButton> decorationButtons = new();

        // 按钮Marker组件缓存
        public static Dictionary<string?, DecorationButtonMarker?> buttonMarkers = new();

        // 按标签分类的滚动视图元素字典
        private static Dictionary<string, List<REPOScrollViewElement>> tagScrollViewElements = new();

        // 标签筛选器
        private static string currentTagFilter = "ALL";
        private static Dictionary<string, REPOButton> tagFilterButtons = new();

        // 装饰物数据缓存 - 存储所有标签的装饰物数据
        private static Dictionary<string, List<DecorationInfo>> decorationDataCache = new();

        // 按钮数据缓存 - 存储按钮文本和状态，避免重复计算
        private static Dictionary<string, Dictionary<string, string>> buttonTextCache = new();

        // 头像预览组件
        private static REPOAvatarPreview? avatarPreview;

        // 大厅按钮位置配置
        private static ConfigEntry<float>? lobbyButtonPosX;
        private static ConfigEntry<float>? lobbyButtonPosY;

        // ESC菜单按钮位置配置
        private static ConfigEntry<float>? escButtonPosX;
        private static ConfigEntry<float>? escButtonPosY;

        // 装备方案显示名称配置
        private static ConfigEntry<string>? outfitLabel1;
        private static ConfigEntry<string>? outfitLabel2;
        private static ConfigEntry<string>? outfitLabel3;
        private static ConfigEntry<string>? outfitLabel4;
        private static ConfigEntry<string>? outfitLabel5;
        private static ConfigEntry<string>? outfitLabel6;
        private static ConfigEntry<string>? outfitLabel7;
        private static ConfigEntry<string>? outfitLabel8;
        private static ConfigEntry<string>? outfitLabel9;

        // 按钮和页面名称常量
        private const string BUTTON_NAME = "<color=#FF0000>M</color><color=#FF3300>O</color><color=#FF6600>R</color><color=#FF9900>E</color><color=#FFCC00>H</color><color=#FFDD00>E</color><color=#FFEE00>A</color><color=#FFFF00>D</color>";
        private static readonly string PAGE_TITLE = $"A/D:Rotate | Ctrl:Fav | Alt:Hide <size=10><color=#AAAAAA>v{Morehead.GetPluginVersion()}</color></size>";

        // 所有可用标签 (with FAV and HIDE)
        private static readonly string[] ALL_TAGS = { "ALL", "FAV", "HEAD", "NECK", "BODY", "HIP", "LIMBS", "WORLD", "HIDE" };

        // 完整的标签列表（包含四肢分类）
        private static readonly string[] FULL_TAGS = { "ALL", "FAV", "HEAD", "NECK", "BODY", "HIP", "LEFTARM", "RIGHTARM", "LEFTLEG", "RIGHTLEG", "WORLD", "HIDE" };

        // 四肢标签
        private static readonly string[] LIMB_TAGS = { "LEFTARM", "RIGHTARM", "LEFTLEG", "RIGHTLEG" };

        // 装备方案按钮字典
        private static Dictionary<int, REPOButton> outfitButtons = new Dictionary<int, REPOButton>();

        private static Dictionary<int, REPOInputField> outfitInputFields = new Dictionary<int, REPOInputField>();

        // 装饰物搜索字段
        private static REPOInputField? decorationSearchField;
        private static REPOButton? searchClearButton;
        private static string currentSearchText = "";
        private static bool isSearching = false;
        private static List<DecorationInfo> searchResults = new();

        // 初始化UI
        public static void Initialize()
        {
            try
            {
                // 初始化黑名单模式配置
                _blacklistModeKey = Morehead.Instance?.Config.Bind(
                    "Blacklist",
                    "BlacklistMode",
                    "",
                    "黑名单模式：设置为 \"ENABLE_BLACKLIST\"（不含引号）以启用。\n" +
                    "启用后可通过 Shift + 点击装饰物按钮 添加/移除 黑名单项目。\n" +
                    "使用 Shift + CLEAR ALL 可一次性清空所有黑名单条目。\n" +
                    "⚠️ 所有黑名单相关操作需重启游戏后才会生效。\n\n" +
                    "Blacklist mode: set to \"ENABLE_BLACKLIST\" to enable.\n" +
                    "When enabled, use Shift + click on decoration buttons to add/remove items from the blacklist.\n" +
                    "Use Shift + CLEAR ALL to clear the entire blacklist at once.\n" +
                    "⚠️ All blacklist changes take effect after restarting the game."
                );


                // 初始化ESC菜单按钮位置配置
                escButtonPosX = Morehead.Instance?.Config.Bind(
                    "UI",
                    "EscButtonPosX",
                    0f,
                    new ConfigDescription("ESC menu button X position", new AcceptableValueRange<float>(0f, 618f))
                );

                escButtonPosY = Morehead.Instance?.Config.Bind(
                    "UI",
                    "EscButtonPosY",
                    0f,
                    new ConfigDescription("ESC menu button Y position", new AcceptableValueRange<float>(0f, 360f))
                );

                // 初始化大厅按钮位置配置
                lobbyButtonPosX = Morehead.Instance?.Config.Bind(
                    "UI",
                    "LobbyButtonPosX",
                    0f,
                    new ConfigDescription("Lobby button X position", new AcceptableValueRange<float>(0f, 618f))
                );

                lobbyButtonPosY = Morehead.Instance?.Config.Bind(
                    "UI",
                    "LobbyButtonPosY",
                    0f,
                    new ConfigDescription("Lobby button Y position", new AcceptableValueRange<float>(0f, 360f))
                );

                var mainMenuButtonPosX = Morehead.Instance?.Config.Bind(
                    "UI",
                    "MainMenuButtonPosX",
                    618f,
                    new ConfigDescription("Main menu button X position", new AcceptableValueRange<float>(0f, 618f))
                );

                var mainMenuButtonPosY = Morehead.Instance?.Config.Bind(
                    "UI",
                    "MainMenuButtonPosY",
                    0f,
                    new ConfigDescription("Main menu button Y position", new AcceptableValueRange<float>(0f, 360f))
                );

                outfitLabel1 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit1Label",
                    "",
                    "Display name for preset F1. Leave empty to show '1'."
                );
                outfitLabel2 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit2Label",
                    "",
                    "Display name for preset F2. Leave empty to show '2'."
                );
                outfitLabel3 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit3Label",
                    "",
                    "Display name for preset F3. Leave empty to show '3'."
                );
                outfitLabel4 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit4Label",
                    "",
                    "Display name for preset F4. Leave empty to show '4'."
                );
                outfitLabel5 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit5Label",
                    "",
                    "Display name for preset F5. Leave empty to show '5'."
                );
                outfitLabel6 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit6Label",
                    "",
                    "Display name for preset F6. Leave empty to show '6'."
                );
                outfitLabel7 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit7Label",
                    "",
                    "Display name for preset F7. Leave empty to show '7'."
                );
                outfitLabel8 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit8Label",
                    "",
                    "Display name for preset F8. Leave empty to show '8'."
                );
                outfitLabel9 = Morehead.Instance?.Config.Bind(
                    "UI",
                    "Outfit9Label",
                    "",
                    "Display name for preset F9. Leave empty to show '9'."
                );

                // 创建ESC菜单按钮
                MenuAPI.AddElementToEscapeMenu(parent =>
                {
                    Vector2 buttonPos = new Vector2(
                        escButtonPosX?.Value ?? 0f,
                        escButtonPosY?.Value ?? 0f
                    );
                    MenuAPI.CreateREPOButton(BUTTON_NAME, OnMenuButtonClick, parent, buttonPos);
                });

                // 创建大厅按钮
                MenuAPI.AddElementToLobbyMenu(parent =>
                {
                    Vector2 buttonPos = new Vector2(
                        lobbyButtonPosX?.Value ?? 0f,
                        lobbyButtonPosY?.Value ?? 0f
                    );
                    MenuAPI.CreateREPOButton(BUTTON_NAME, OnMenuButtonClick, parent, buttonPos);
                });

                MenuAPI.AddElementToMainMenu(parent =>
                {
                    Vector2 buttonPos = new Vector2(
                        mainMenuButtonPosX?.Value ?? 0f,
                        mainMenuButtonPosY?.Value ?? 0f
                    );
                    MenuAPI.CreateREPOButton(BUTTON_NAME, OnMenuButtonClick, parent, buttonPos);
                });

                // 初始化数据缓存
                InitializeDataCache();

                Logger?.LogInfo("MoreHead UI已初始化");
            }
            catch (Exception e)
            {
                Logger?.LogError($"初始化UI时出错: {e.Message}");
            }
        }

        // 初始化数据缓存
        private static void InitializeDataCache()
        {
            try
            {
                // 清空现有缓存
                decorationDataCache.Clear();
                buttonTextCache.Clear();

                // 为每个标签创建数据缓存
                foreach (string tag in ALL_TAGS)
                {
                    List<DecorationInfo> filteredDecorations;

                    if (tag == "LIMBS")
                    {
                        filteredDecorations = HeadDecorationManager.Decorations
                            .Where(decoration => LIMB_TAGS.Contains(decoration.ParentTag?.ToUpper()))
                            .ToList();
                    }
                    else if (tag == "FAV")
                    {
                        filteredDecorations = HeadDecorationManager.Decorations
                            .Where(decoration => FavoritesManager.IsFavorite(decoration.DisplayName ?? string.Empty))
                            .ToList();
                    }
                    else if (tag == "HIDE")
                    {
                        filteredDecorations = HeadDecorationManager.Decorations
                            .Where(decoration => FavoritesManager.IsHidden(decoration.DisplayName ?? string.Empty))
                            .ToList();
                    }
                    else
                    {
                        filteredDecorations = HeadDecorationManager.Decorations
                            .Where(decoration => tag == "ALL" || (decoration.ParentTag?.ToUpper() == tag))
                            .ToList();
                    }

                    // 添加到缓存
                    decorationDataCache[tag] = filteredDecorations;

                    // 为每个标签创建按钮文本缓存
                    buttonTextCache[tag] = new Dictionary<string, string>();

                    // 预先计算并缓存所有按钮文本
                    foreach (var decoration in filteredDecorations)
                    {
                        string buttonText = GetButtonText(decoration, decoration.IsVisible);
                        buttonTextCache[tag][decoration.Name ?? string.Empty] = buttonText;
                    }
                }

                Logger?.LogInfo("数据缓存初始化完成");
            }
            catch (Exception e)
            {
                Logger?.LogError($"初始化数据缓存时出错: {e.Message}");
            }
        }

        // 主菜单按钮点击事件
        private static void OnMenuButtonClick()
        {
            try
            {
                // 检查是否有ESC菜单打开，如果有则只关闭ESC菜单
                if (MenuPageEsc.instance != null)
                {
                    // 调用ESC菜单的继续按钮功能来关闭菜单
                    MenuPageEsc.instance.ButtonEventContinue();
                }

                // 添加延迟确保所有UI已关闭
                UnityEngine.MonoBehaviour.FindObjectOfType<MonoBehaviour>()?.StartCoroutine(
                    DelayedOpenMoreHeadUI());
            }
            catch (Exception e)
            {
                Logger?.LogError($"打开设置页面时出错: {e.Message}");
            }
        }

        // 延迟打开MoreHead UI，确保先关闭所有其他UI
        private static System.Collections.IEnumerator DelayedOpenMoreHeadUI()
        {
            // 等待一帧，确保PageCloseAll执行完毕
            yield return null;

            try
            {
                // 如果装饰页面还没创建，则创建它
                if (decorationsPage == null)
                {
                    // 创建新页面并启用缓存
                    //Logger?.LogInfo("创建新页面");
                    decorationsPage = MenuAPI.CreateREPOPopupPage(PAGE_TITLE, true, true, 0, new Vector2(-299, 10));

                    // 设置页面属性
                    SetupPopupPage(decorationsPage);

                    // 创建所有装饰物按钮
                    CreateAllDecorationButtons(decorationsPage);

                    // 创建标签筛选按钮
                    CreateTagFilterButtons(decorationsPage);

                    // 添加作者标记
                    AddAuthorCredit(decorationsPage);

                    // 添加操作按钮（关闭、清除所有）
                    AddActionButtons(decorationsPage);
                }

                ReorderAllButtonsBySorting();

                // 打开页面
                decorationsPage.OpenPage(false);

                // 延迟一帧显示当前标签的装饰物
                UnityEngine.MonoBehaviour.FindObjectOfType<MonoBehaviour>()?.StartCoroutine(
                    DelayedShowTagDecorations(currentTagFilter));

                // 创建或移动头像预览
                UpdateAvatarPreview();

                // 更新所有按钮状态
                UpdateButtonStates();
            }
            catch (Exception e)
            {
                Logger?.LogError($"延迟打开设置页面时出错: {e.Message}");
            }
        }

        // 设置弹出页面属性
        private static void SetupPopupPage(REPOPopupPage page)
        {
            try
            {
                // 设置页面名称
                if (page.gameObject != null)
                {
                    page.gameObject.name = "MoreHead_Page";
                }

                // 设置页面大小和位置
                //page.rectTransform.sizeDelta = new Vector2(300f, 350f);
                page.pageDimmerVisibility = true;
                page.maskPadding = new Padding(10f, 10f, 20f, 10f);
                page.headerTMP.rectTransform.position = new Vector3(170, 370, 0);
                page.pageDimmerOpacity = 0.85f;
                page.scrollView.scrollSpeed = 4f;
            }
            catch (Exception e)
            {
                Logger?.LogError($"设置弹出页面属性时出错: {e.Message}");
            }
        }

        // 添加作者标记
        private static void AddAuthorCredit(REPOPopupPage page)
        {
            try
            {
                // 创建作者标记按钮（使用按钮作为文本显示，但不添加点击事件）
                page.AddElement(parent =>
                {
                    // 获取Steam客户端语言，先检查Steam是否已初始化
                    string? steamLanguage = null;
                    try
                    {
                        // 尝试获取Steam语言，如果Steam未初始化会抛出异常
                        if (SteamClient.IsValid)
                        {
                            steamLanguage = SteamUtils.SteamUILanguage;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Steam未初始化或获取语言失败，记录错误但不影响功能
                        Logger?.LogWarning($"获取Steam语言时出错（将使用默认英文）: {ex.Message}");
                    }

                    string authorText;

                    // 根据Steam客户端语言决定显示文本
                    if (steamLanguage != null && steamLanguage.ToLower().StartsWith("schinese"))
                    {
                        // 简体中文
                        authorText = "<size=10>由<color=#FFFFA0>马赛克了</color>和<color=#FFFFA0>尤里的猫</color>共同制作。</size>";
                    }
                    else if (steamLanguage != null && steamLanguage.ToLower().StartsWith("tchinese"))
                    {
                        // 繁体中文
                        authorText = "<size=10>由<color=#FFFFA0>馬賽克了</color>和<color=#FFFFA0>尤里的貓</color>共同製作。</size>";
                    }
                    else
                    {
                        // 其他语言或Steam未初始化时只显示英文
                        authorText = "<size=10><color=#FFFFA0>Masaicker</color> and <color=#FFFFA0>Yuriscat</color> co-developed.</size>";
                    }

                    MenuAPI.CreateREPOButton(authorText, () => { }, parent, new Vector2(300, 339));
                });
            }
            catch (Exception e)
            {
                Logger?.LogError($"添加作者标记时出错: {e.Message}");
            }
        }

        // 判断是否为内置模型
        private static bool IsBuiltInDecoration(DecorationInfo decoration)
        {
            // 内置模型的源路径中会包含MOD的主要目录路径下的Decorations文件夹
            // 而不是外部DLL中的资源
            string? bundlePath = decoration.BundlePath;
            if (string.IsNullOrEmpty(bundlePath))
                return false;

            // 获取MOD所在目录和装饰物目录
            string? modDirectory = Path.GetDirectoryName(Morehead.Instance?.Info.Location);
            if (string.IsNullOrEmpty(modDirectory))
                return false;

            string decorationsDirectory = Path.Combine(modDirectory, "Decorations");

            // 判断路径是否在MOD的Decorations目录下
            return bundlePath.StartsWith(decorationsDirectory);
        }

        // 创建标签筛选按钮
        private static void CreateTagFilterButtons(REPOPopupPage? page)
        {
            try
            {
                // 清空标签按钮字典
                tagFilterButtons.Clear();

                // 创建装备方案切换按钮
                CreateOutfitButtons(page);

                CreateOutfitInputFields(page);

                CreateDecorationSearchField(page);

                // 标签按钮的水平间距
                const int buttonSpacing = 35; // 减小间距，使按钮靠近一点
                // 起始X坐标
                const int startX = 50;
                // Y坐标
                const int y = 20; // 向上移动，避免与其他按钮重叠

                // 为每个标签创建按钮
                for (int i = 0; i < ALL_TAGS.Length; i++)
                {
                    string tag = ALL_TAGS[i];
                    string lowerTag = tag.ToLower();
                    int index = i; // 捕获循环变量

                    // 标签颜色（与GetButtonText方法中的颜色保持一致）
                    string tagColor = lowerTag switch
                    {
                        "fav" => "#FFD700",  // Gold for favorites
                        "hide" => "#666666", // Gray for hidden
                        "head" => "#00AAFF", // 蓝色
                        "neck" => "#AA00FF", // 紫色
                        "body" => "#FFAA00", // 橙色
                        "hip" => "#FF00AA", // 粉色
                        "limbs" => "#AACCAA", // 淡绿色（四肢页面）
                        "world" => "#00FFAA", // 青色
                        _ => "#FFFFFF"       // 白色（ALL标签）
                    };

                    // Special display for FAV and HIDE
                    string displayTag = lowerTag switch
                    {
                        "fav" => "[FAV]",
                        "hide" => "[HIDE]",
                        _ => tag
                    };

                    // 如果是当前选中的标签，则使用更亮的颜色和加粗效果
                    string buttonText = lowerTag == currentTagFilter.ToLower() ?
                        $"<size=13><u><color={tagColor}>{displayTag}</color></u></size>" :
                        $"<size=13><color={tagColor}50>{displayTag}</color></size>";

                    // 创建按钮 - 确保标签大小写一致
                    string tagForCallback = lowerTag == "all" ? "ALL" : tag;

                    // 计算按钮位置
                    int xPosition = startX + i * buttonSpacing;

                    page?.AddElement(parent =>
                    {
                        var button = MenuAPI.CreateREPOButton(
                            buttonText,
                            () => OnTagFilterButtonClick(tagForCallback),
                            parent,
                            new Vector2(xPosition, y)
                        );

                        // 添加到标签按钮字典
                        tagFilterButtons[tagForCallback] = button;
                    });
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建标签筛选按钮时出错: {e.Message}");
            }
        }

        // 标签筛选按钮点击事件
        private static void OnTagFilterButtonClick(string tag)
        {
            try
            {
                string oldTag = currentTagFilter;
                if (isSearching || tag != currentTagFilter)
                {
                    ReorderAllButtonsBySorting();
                    ShowTagDecorations2(tag);
                }

                if (isSearching)
                {
                    ClearSearchField();
                }

                if (tag == oldTag)
                {
                    decorationsPage?.scrollView.SetScrollPosition(0);
                    return;
                }

                // 然后更新标签按钮高亮状态
                UpdateTagButtonHighlights();

                // 确保按钮状态正确显示
                UpdateButtonStates();
            }
            catch (Exception e)
            {
                Logger?.LogError($"切换标签筛选时出错: {e.Message}");
            }
        }

        // 更新所有按钮状态
        private static void UpdateButtonStates()
        {
            try
            {
                // 如果按钮字典为空，不需要更新
                if (decorationButtons.Count == 0)
                {
                    return;
                }

                // 获取当前标签的装饰物
                if (decorationDataCache.TryGetValue(currentTagFilter, out var decorations))
                {
                    // 更新每个装饰物按钮
                    foreach (var decoration in decorations)
                    {
                        if (decorationButtons.TryGetValue(decoration.Name ?? string.Empty, out REPOButton button))
                        {
                            // 获取缓存的按钮文本或重新计算
                            string buttonText;
                            if (buttonTextCache.TryGetValue(currentTagFilter, out var textCache) &&
                                textCache.TryGetValue(decoration.Name ?? string.Empty, out buttonText))
                            {
                                // 无论是否从缓存获取，都要确保状态是最新的
                                buttonText = GetButtonText(decoration, decoration.IsVisible);

                                // 更新缓存
                                textCache[decoration.Name ?? string.Empty] = buttonText;
                            }
                            else
                            {
                                // 重新计算按钮文本
                                buttonText = GetButtonText(decoration, decoration.IsVisible);

                                // 更新缓存
                                if (buttonTextCache.TryGetValue(currentTagFilter, out textCache))
                                {
                                    textCache[decoration.Name ?? string.Empty] = buttonText;
                                }
                            }

                            button.labelTMP.text = buttonText;
                        }
                    }
                }

                // 更新标签按钮高亮状态
                UpdateTagButtonHighlights();

                // 更新装备方案按钮高亮状态
                // UpdateOutfitButtonHighlights();
            }
            catch (Exception e)
            {
                Logger?.LogError($"更新按钮状态时出错: {e.Message}");
            }
        }

        private static void ReorderAllButtonsBySorting()
        {
            try
            {
                if (decorationsPage == null || decorationButtons.Count == 0)
                    return;

                var sorted = DecorationSortingExtension
                    .ApplySorting(HeadDecorationManager.Decorations)
                    .ToList();

                int index = 0;
                foreach (var d in sorted)
                {
                    if (d?.Name == null) continue;
                    if (decorationButtons.TryGetValue(d.Name, out var btn))
                    {
                        btn.rectTransform.SetSiblingIndex(index++);
                    }
                }

                decorationsPage.scrollView.UpdateElements();
            }
            catch (Exception e)
            {
                Logger?.LogWarning($"重排按钮顺序时出错（可忽略）: {e.Message}");
            }
        }

        // 更新标签按钮高亮状态
        private static void UpdateTagButtonHighlights()
        {
            try
            {
                // 更新每个标签按钮的高亮状态
                foreach (string tag in ALL_TAGS)
                {
                    // 确保标签格式一致
                    string tagKey = tag == "ALL" ? "ALL" : tag;

                    if (tagFilterButtons.TryGetValue(tagKey, out REPOButton button))
                    {
                        // 标签颜色
                        string tagColor = tag.ToLower() switch
                        {
                            "fav" => "#FFD700",  // Gold for favorites
                            "hide" => "#666666", // Gray for hidden
                            "head" => "#00AAFF", // 蓝色
                            "neck" => "#AA00FF", // 紫色
                            "body" => "#FFAA00", // 橙色
                            "hip" => "#FF00AA", // 粉色
                            "limbs" => "#AACCAA", // 淡绿色（四肢页面）
                            "world" => "#00FFAA", // 青色
                            _ => "#FFFFFF"       // 白色（ALL标签）
                        };

                        // Special display for FAV and HIDE
                        string displayTag = tag.ToLower() switch
                        {
                            "fav" => "[FAV]",
                            "hide" => "[HIDE]",
                            _ => tag
                        };

                        // 如果是当前选中的标签，则使用更亮的颜色和加粗效果
                        string buttonText = tagKey == currentTagFilter ?
                            $"<size=13><u><color={tagColor}>{displayTag}</color></u></size>" :
                            $"<size=13><color={tagColor}50>{displayTag}</color></size>";

                        button.labelTMP.text = buttonText;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"更新标签按钮高亮状态时出错: {e.Message}");
            }
        }

        // 装饰物按钮点击事件
        private static void OnDecorationButtonClick(string? decorationName)
        {
            try
            {
                // 查找装饰物信息
                var decoration = HeadDecorationManager.Decorations.FirstOrDefault(d => d.Name != null && d.Name.Equals(decorationName, StringComparison.OrdinalIgnoreCase));
                if (decoration == null)
                {
                    Logger?.LogWarning($"OnDecorationButtonClick: 找不到装饰物: {decorationName}");
                    return;
                }

                // 检查是否处于黑名单模式
                string blacklistModeKey = _blacklistModeKey?.Value ?? string.Empty;
                bool isBlacklistModeActive = blacklistModeKey == "ENABLE_BLACKLIST";

                // 检查是否按下了shift键
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // 先判断是否处于黑名单模式
                if (isBlacklistModeActive && isShiftPressed)
                {
                    // 确保DisplayName不为空
                    string displayName = decoration.DisplayName ?? string.Empty;

                    // 检查装饰物是否已在黑名单中
                    bool isBlacklisted = DecorationBlacklistManager.IsBlacklisted(displayName);

                    if (isBlacklisted)
                    {
                        // 从黑名单中移除
                        DecorationBlacklistManager.RemoveFromBlacklist(displayName);
                    }
                    else
                    {
                        // 添加到黑名单
                        DecorationBlacklistManager.AddToBlacklist(displayName);
                    }

                    // 刷新UI状态
                    UpdateButtonStates();
                    return;
                }

                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool isAltPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                string displayName2 = decoration.DisplayName ?? string.Empty;

                // Ctrl = Toggle FAV
                if (isCtrlPressed)
                {
                    if (FavoritesManager.IsFavorite(displayName2))
                    {
                        FavoritesManager.RemoveFavorite(displayName2);

                        if (decorationButtons.TryGetValue(decorationName, out var btn))
                        {
                            tagScrollViewElements["FAV"].Remove(btn.repoScrollViewElement);

                            btn.labelTMP.text = GetButtonText(decoration, decoration.IsVisible);

                            if (currentTagFilter == "FAV")
                            {
                                decorationsPage?.scrollView.UpdateElements();
                            }
                        }
                    }
                    else
                    {
                        FavoritesManager.AddFavorite(displayName2);

                        if (decorationButtons.TryGetValue(decorationName, out var btn))
                        {
                            tagScrollViewElements["FAV"].Add(btn.repoScrollViewElement);

                            btn.labelTMP.text = GetButtonText(decoration, decoration.IsVisible);

                            if (currentTagFilter == "FAV")
                            {
                                btn.repoScrollViewElement.visibility = true;
                                decorationsPage?.scrollView.UpdateElements();
                            }
                        }
                    }

                    // ReorderAllButtonsBySorting();
                    UpdateButtonStates();

                    if (decorationButtons.TryGetValue(decorationName, out var clickedBtn) &&
                        buttonMarkers.TryGetValue(decorationName, out var marker) &&
                        marker?.Decoration != null &&
                        !string.IsNullOrEmpty(marker.Decoration.ModName))
                    {
                        if (!clickedBtn.labelTMP.text.Contains(marker.Decoration.ModName))
                        {
                            clickedBtn.labelTMP.text = $"{clickedBtn.labelTMP.text} <size=12><color=#AAAAAA>- {marker.Decoration.ModName}</color></size>";
                        }
                    }
                    return;
                }

                // Alt = Toggle HIDE
                if (isAltPressed)
                {
                    bool wasHidden = FavoritesManager.IsHidden(displayName2);

                    if (wasHidden)
                    {
                        FavoritesManager.RemoveHidden(displayName2);

                        if (decorationButtons.TryGetValue(decorationName, out var btn))
                        {
                            tagScrollViewElements["HIDE"].Remove(btn.repoScrollViewElement);

                            btn.labelTMP.text = GetButtonText(decoration, decoration.IsVisible);

                            if (currentTagFilter == "HIDE")
                            {
                                decorationsPage?.scrollView.UpdateElements();
                            }

                            string parentTag = decoration?.ParentTag?.ToUpper() ?? "";
                            if (!string.IsNullOrEmpty(parentTag))
                            {
                                tagScrollViewElements["ALL"].Add(btn.repoScrollViewElement);

                                if (LIMB_TAGS.Contains(parentTag))
                                {
                                    tagScrollViewElements["LIMBS"].Add(btn.repoScrollViewElement);
                                }
                                else if (tagScrollViewElements.ContainsKey(parentTag))
                                {
                                    tagScrollViewElements[parentTag].Add(btn.repoScrollViewElement);
                                }
                            }
                        }
                    }
                    else
                    {
                        FavoritesManager.AddHidden(displayName2);

                        if (decorationButtons.TryGetValue(decorationName, out var btn))
                        {
                            tagScrollViewElements["HIDE"].Add(btn.repoScrollViewElement);

                            btn.labelTMP.text = GetButtonText(decoration, decoration.IsVisible);

                            if (currentTagFilter == "HIDE")
                            {
                                btn.repoScrollViewElement.visibility = true;
                                decorationsPage?.scrollView.UpdateElements();
                            }

                            string parentTag = decoration?.ParentTag?.ToUpper() ?? "";
                            tagScrollViewElements["ALL"].Remove(btn.repoScrollViewElement);

                            if (LIMB_TAGS.Contains(parentTag))
                            {
                                tagScrollViewElements["LIMBS"].Remove(btn.repoScrollViewElement);
                            }
                            else if (!string.IsNullOrEmpty(parentTag) && tagScrollViewElements.ContainsKey(parentTag))
                            {
                                tagScrollViewElements[parentTag].Remove(btn.repoScrollViewElement);
                            }
                        }
                    }

                    // ReorderAllButtonsBySorting();
                    UpdateButtonStates();

                    if (decorationButtons.TryGetValue(decorationName, out var clickedBtn) &&
                        buttonMarkers.TryGetValue(decorationName, out var marker) &&
                        marker?.Decoration != null &&
                        !string.IsNullOrEmpty(marker.Decoration.ModName))
                    {
                        if (!clickedBtn.labelTMP.text.Contains(marker.Decoration.ModName))
                        {
                            clickedBtn.labelTMP.text = $"{clickedBtn.labelTMP.text} <size=12><color=#AAAAAA>- {marker.Decoration.ModName}</color></size>";
                        }
                    }
                    return;
                }

                string displayNameForNew = decoration.DisplayName ?? string.Empty;
                if (NewItemsManager.IsNew(displayNameForNew))
                {
                    NewItemsManager.MarkAsViewed(displayNameForNew);
                }

                // 正常操作：切换装饰物状态
                bool newState = HeadDecorationManager.ToggleDecorationState(decorationName);

                // 更新按钮文本缓存
                string newButtonText = GetButtonText(decoration, newState);

                // 更新所有标签的按钮文本缓存
                foreach (string tag in ALL_TAGS)
                {
                    bool shouldShow = false;

                    if (tag == "ALL")
                    {
                        shouldShow = true;
                    }
                    else if (tag == "LIMBS" && LIMB_TAGS.Contains(decoration.ParentTag?.ToUpper()))
                    {
                        shouldShow = true;
                    }
                    else if (decoration.ParentTag?.ToUpper() == tag)
                    {
                        shouldShow = true;
                    }

                    if (shouldShow && buttonTextCache.TryGetValue(tag, out var textCache))
                    {
                        textCache[decorationName ?? string.Empty] = newButtonText;
                    }
                }

                // 更新当前页面上的按钮状态
                if (decorationButtons.TryGetValue(decorationName ?? string.Empty, out REPOButton button))
                {
                    // 更新按钮文本以反映新状态
                    button.labelTMP.text = newButtonText;

                    // 使用缓存的Marker组件
                    if (buttonMarkers.TryGetValue(decorationName ?? string.Empty, out var marker) &&
                        marker?.Decoration != null &&
                        !string.IsNullOrEmpty(marker.Decoration.ModName))
                    {
                        // 检查当前文本是否已经包含模组名称
                        if (!button.labelTMP.text.Contains(marker.Decoration.ModName))
                        {
                            // 添加模组名称到按钮文本末尾
                            button.labelTMP.text = $"{button.labelTMP.text} <size=12><color=#AAAAAA>- {marker.Decoration.ModName}</color></size>";
                        }
                    }
                }

                // 更新玩家装饰物
                UpdateDecorations();
                // 保存配置
                ConfigManager.SaveConfig();
            }
            catch (Exception e)
            {
                Logger?.LogError($"切换装饰物 {decorationName} 状态时出错: {e.Message}");
            }
        }

        // 获取按钮文本
        private static string GetButtonText(DecorationInfo decoration, bool isEnabled)
        {
            // 获取装饰物名称和标签
            string name = decoration.DisplayName?.ToUpper() ?? "UNKNOWN";
            string parentTag = decoration.ParentTag ?? "unknown";

            string displayName = decoration.DisplayName ?? string.Empty;

            bool isFavorite = FavoritesManager.IsFavorite(displayName);
            bool isHidden = FavoritesManager.IsHidden(displayName);
            bool isNew = NewItemsManager.IsNew(displayName);

            string favHideSuffix = "";
            if (isNew)
            {
                favHideSuffix = "<color=#008b09>[NEW]</color> ";
            }
            if (isFavorite)
            {
                favHideSuffix += "<color=#FFD700>[FAV]</color> ";
            }
            if (isHidden)
            {
                favHideSuffix += "<color=#666666>[HIDE]</color> ";
            }

            // 为不同标签设置不同颜色（避免使用红色和绿色，因为已用于ON/OFF）
            string tagColor = parentTag.ToLower() switch
            {
                "head" => "#00AAFF", // 蓝色
                "neck" => "#AA00FF", // 紫色
                "body" => "#FFAA00", // 橙色
                "hip" => "#FF00AA", // 粉色
                "leftarm" => "#88CC88", // 淡绿色（手臂）
                "rightarm" => "#88CC88", // 淡绿色（手臂）
                "leftleg" => "#88BBEE", // 淡蓝色（腿部）
                "rightleg" => "#88BBEE", // 淡蓝色（腿部）
                "world" => "#00FFAA", // 青色
                _ => "#AAAAAA"       // 灰色（未知标签）
            };

            // 构建子标签显示（用于四肢标签下显示具体是哪个肢体）
            string subTagDisplay = "";
            if (LIMB_TAGS.Contains(parentTag.ToUpper()))
            {
                string subTagText = parentTag.ToLower() switch
                {
                    "leftarm" => "L-ARM",
                    "rightarm" => "R-ARM",
                    "leftleg" => "L-LEG",
                    "rightleg" => "R-LEG",
                    _ => parentTag
                };
                subTagDisplay = $"<color={tagColor}><size=12>({subTagText})</size></color> ";
            }
            else
            {
                subTagDisplay = $"<color={tagColor}><size=12>({parentTag})</size></color> ";
            }

            // 检查是否处于黑名单模式
            string blacklistModeKey = _blacklistModeKey?.Value ?? string.Empty;
            bool isBlacklistModeActive = blacklistModeKey == "ENABLE_BLACKLIST";

            // 处理黑名单显示效果
            // 只有在黑名单模式下才检查物品是否被拉黑
            if (isBlacklistModeActive)
            {
                // 检查是否在黑名单中
                bool isBlacklisted = DecorationBlacklistManager.IsBlacklisted(decoration.DisplayName ?? string.Empty);

                // 如果该物品在黑名单中，则使用删除线和灰色显示
                if (isBlacklisted)
                {
                    // 使用删除线和灰色显示名称
                    string grayTagColor = "#888888"; // 灰色
                    subTagDisplay = $"<color={grayTagColor}><size=12>({parentTag})</size></color> ";
                    return $"<s><size=16>{(isEnabled ? "<color=#888888>[+]</color>" : "<color=#888888>[-]</color>")} {favHideSuffix}{subTagDisplay}<color=#888888>{name}</color></size></s>";
                }
            }

            // Apply hidden styling (dimmed) if item is hidden
            if (isHidden && currentTagFilter != "HIDE")
            {
                return $"<size=16>{(isEnabled ? "<color=#00FF00>[+]</color>" : "<color=#FF0000>[-]</color>")} {favHideSuffix}{subTagDisplay}<color=#888888>{name}</color></size>";
            }

            // 返回格式化的按钮文本（非黑名单物品）
            return $"<size=16>{(isEnabled ? "<color=#00FF00>[+]</color>" : "<color=#FF0000>[-]</color>")} {favHideSuffix}{subTagDisplay}{name}</size>";
        }

        // 关闭按钮点击事件
        private static void OnCloseButtonClick()
        {
            try
            {
                ClearSearchField();
                decorationsPage?.ClosePage(true);
            }
            catch (Exception e)
            {
                Logger?.LogError($"关闭页面时出错: {e.Message}");
            }
        }

        // 更新所有装饰物状态
        private static void UpdateDecorations()
        {
            try
            {
                // 查找本地玩家
                var localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    // 更新本地玩家的装饰物状态
                    PlayerUpdatePatch.UpdatePlayerDecorations(localPlayer);

                    // 通过RPC同步到其他玩家
                    var syncComponent = localPlayer.GetComponent<HeadDecorationSync>();
                    if (syncComponent != null)
                    {
                        // 使用增量同步方法，只同步已变化的装饰物
                        syncComponent.SyncChangedDecorations();
                    }
                }

                // 更新菜单角色的装饰物状态
                PlayerUpdatePatch.UpdateMenuPlayerDecorations();
            }
            catch (Exception e)
            {
                Logger?.LogError($"更新装饰物状态时出错: {e.Message}");
            }
        }

        // 查找本地玩家
        private static PlayerAvatar? FindLocalPlayer()
        {
            try
            {
                // 查找所有PlayerAvatar对象
                PlayerAvatar?[]? playerAvatars = UnityEngine.Object.FindObjectsOfType<PlayerAvatar>();
                foreach (var avatar in playerAvatars)
                {
                    // 检查是否是本地玩家
                    if (avatar?.photonView != null && avatar.photonView.IsMine)
                    {
                        return avatar;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"查找本地玩家时出错: {e.Message}");
            }

            return null;
        }

        // 更新/创建头像预览
        private static void UpdateAvatarPreview()
        {
            try
            {
                // 如果页面不存在，不做任何操作
                if (decorationsPage == null)
                {
                    return;
                }

                // 如果已存在头像预览，先销毁它
                if (avatarPreview != null)
                {
                    SafeDestroyAvatar();
                }

                // 创建新的头像预览
                CreateAvatarPreview(decorationsPage);
            }
            catch (Exception e)
            {
                Logger?.LogError($"更新头像预览时出错: {e.Message}");
            }
        }

        // 创建头像预览
        private static void CreateAvatarPreview(REPOPopupPage page)
        {
            try
            {
                // 在页面上创建角色预览
                page.AddElement(parent =>
                {
                    // 创建角色预览组件
                    avatarPreview = MenuAPI.CreateREPOAvatarPreview(
                        parent,
                        new Vector2(420, 10),  // 预览位置
                        false                  // 默认不启用背景图片
                    );
                });

                // 标记为已创建

                //Logger?.LogInfo("成功创建头像预览");
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建头像预览时出错: {e.Message}");
            }
        }

        // 安全销毁头像预览
        public static void SafeDestroyAvatar()
        {
            try
            {
                if (avatarPreview != null)
                {
                    // 先将其从父级分离，避免OnDestroy时引用父级对象可能导致的问题
                    if (avatarPreview.transform != null && avatarPreview.transform.parent != null)
                    {
                        avatarPreview.transform.SetParent(null, false);
                    }

                    // 主动清理预览对象内部的引用，避免OnDestroy时的空引用
                    var playerAvatarVisuals = avatarPreview.playerAvatarVisuals;
                    if (playerAvatarVisuals != null)
                    {
                        // 清理可能的引用，防止预览对象被销毁时抛出异常
                        // 这里不做具体操作，只是防止空引用
                    }

                    // 销毁游戏对象
                    if (avatarPreview.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(avatarPreview.gameObject);
                    }

                    // 避免后续引用已销毁的对象
                    avatarPreview = null;
                }
            }
            catch (Exception e)
            {
                Logger?.LogWarning($"安全销毁头像预览时出错，但这不影响功能: {e.Message}");
                avatarPreview = null;
            }
        }

        // "关闭所有模型"按钮点击事件
        private static void OnDisableAllButtonClick()
        {
            try
            {
                // 检查是否处于黑名单模式
                string blacklistModeKey = _blacklistModeKey?.Value ?? string.Empty;
                bool isBlacklistModeActive = blacklistModeKey == "ENABLE_BLACKLIST";

                // 检查是否按下了shift键
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // 在黑名单模式下按住Shift键，清空黑名单
                if (isBlacklistModeActive && isShiftPressed)
                {
                    // 清空黑名单
                    DecorationBlacklistManager.ClearBlacklist();

                    // 更新所有按钮状态
                    UpdateButtonStates();

                    return;
                }

                // 关闭所有装饰物
                HeadDecorationManager.DisableAllDecorations();

                // 更新所有标签的按钮文本缓存
                foreach (string tag in ALL_TAGS)
                {
                    if (buttonTextCache.TryGetValue(tag, out var textCache))
                    {
                        foreach (var decoration in HeadDecorationManager.Decorations)
                        {
                            bool shouldShow = false;

                            if (tag == "ALL")
                            {
                                shouldShow = true;
                            }
                            else if (tag == "LIMBS" && LIMB_TAGS.Contains(decoration.ParentTag?.ToUpper()))
                            {
                                shouldShow = true;
                            }
                            else if (decoration.ParentTag?.ToUpper() == tag)
                            {
                                shouldShow = true;
                            }

                            if (shouldShow)
                            {
                                // 更新缓存
                                string buttonText = GetButtonText(decoration, false);
                                textCache[decoration.Name ?? string.Empty] = buttonText;
                            }
                        }
                    }
                }

                // 更新当前页面上的按钮状态
                foreach (var decoration in HeadDecorationManager.Decorations)
                {
                    if (decorationButtons.TryGetValue(decoration.Name ?? string.Empty, out REPOButton button))
                    {
                        // 更新按钮文本以反映新状态（全部关闭）
                        button.labelTMP.text = GetButtonText(decoration, false);
                    }
                }

                // 更新玩家装饰物
                UpdateDecorations();

                // 保存配置
                ConfigManager.SaveConfig();
            }
            catch (Exception e)
            {
                Logger?.LogError($"关闭所有装饰物时出错: {e.Message}");
            }
        }

        // 重新创建UI（供第三方MOD使用）
        public static void RecreateUI()
        {
            try
            {
                // 清空所有缓存的数据
                decorationDataCache.Clear();
                buttonTextCache.Clear();
                decorationButtons.Clear();
                buttonMarkers.Clear();
                tagScrollViewElements.Clear();
                tagFilterButtons.Clear();
                outfitButtons.Clear();
                outfitInputFields.Clear();

                currentSearchText = "";
                isSearching = false;
                searchResults.Clear();
                decorationSearchField = null;
                searchClearButton = null;

                // 销毁现有页面
                if (decorationsPage != null && decorationsPage.gameObject != null)
                {
                    UnityEngine.Object.Destroy(decorationsPage.gameObject);
                    decorationsPage = null;
                }

                // 安全销毁头像预览并重置标记
                SafeDestroyAvatar();

                // 重新初始化数据缓存
                InitializeDataCache();

                Logger?.LogInfo("UI已重新初始化，缓存已重置");
            }
            catch (Exception e)
            {
                Logger?.LogError($"重新创建UI时出错: {e.Message}");
            }
        }

        // 创建所有装饰物按钮
        private static void CreateAllDecorationButtons(REPOPopupPage page)
        {
            try
            {
                // 清空现有数据
                decorationButtons.Clear();
                buttonMarkers.Clear();
                tagScrollViewElements.Clear();

                // 为每个标签初始化元素列表
                foreach (string tag in ALL_TAGS)
                {
                    tagScrollViewElements[tag] = new List<REPOScrollViewElement>();
                }

                // 获取所有装饰物并按照优先级排序
                var allDecorations = DecorationSortingExtension
                    .ApplySorting(HeadDecorationManager.Decorations)
                    .ToList();

                // 按照排序后的顺序创建按钮（不再按内置/外部分组）
                foreach (var decoration in allDecorations)
                {
                    CreateDecorationButton(page, decoration);
                }

                Logger?.LogInfo($"创建了所有装饰物按钮，总共 {decorationButtons.Count} 个");
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建装饰物按钮时出错: {e.Message}");
            }
        }

        // 创建单个装饰物按钮
        private static void CreateDecorationButton(REPOPopupPage page, DecorationInfo decoration)
        {
            try
            {
                // 获取装饰物名称和标签
                string? decorationName = decoration.Name;
                string? parentTag = decoration.ParentTag;

                if (string.IsNullOrEmpty(decorationName) || string.IsNullOrEmpty(parentTag))
                {
                    Logger?.LogWarning($"跳过创建按钮：装饰物名称或标签为空");
                    return;
                }

                // 获取或生成按钮文本
                string buttonText = GetButtonText(decoration, decoration.IsVisible);

                // 更新按钮文本缓存
                foreach (string tag in ALL_TAGS)
                {
                    bool shouldCache = false;

                    if (tag == "ALL")
                    {
                        shouldCache = true;
                    }
                    else if (tag == "LIMBS" && LIMB_TAGS.Contains(parentTag.ToUpper()))
                    {
                        shouldCache = true;
                    }
                    else if (parentTag.ToUpper() == tag)
                    {
                        shouldCache = true;
                    }

                    if (shouldCache)
                    {
                        // 确保缓存存在
                        if (!buttonTextCache.TryGetValue(tag, out var textCache))
                        {
                            buttonTextCache[tag] = new Dictionary<string, string>();
                            textCache = buttonTextCache[tag];
                        }

                        // 更新缓存
                        textCache[decorationName] = buttonText;
                    }
                }

                // 获取FAV/HIDE/NEW状态
                string displayName = decoration.DisplayName ?? string.Empty;
                bool isFavorite = FavoritesManager.IsFavorite(displayName);
                bool isHidden = FavoritesManager.IsHidden(displayName);
                bool isNew = NewItemsManager.IsNew(displayName);

                // 创建按钮
                REPOButton? repoButton = null;

                page.AddElementToScrollView(scrollView =>
                {
                    repoButton = MenuAPI.CreateREPOButton(
                        buttonText,
                        () => OnDecorationButtonClick(decorationName),
                        scrollView
                    );

                    var marker = repoButton.gameObject.AddComponent<DecorationButtonMarker>();
                    marker.Decoration = decoration;
                    buttonMarkers[decorationName] = marker;

                    return repoButton.rectTransform;
                });

                // 添加到按钮字典
                if (repoButton != null)
                {
                    decorationButtons[decorationName] = repoButton;

                    // 默认隐藏所有按钮
                    repoButton.repoScrollViewElement.visibility = false;

                    // 将按钮添加到对应的标签分类中
                    // Aqui eu quero que os elementos hide só apareçam no menu hide, os fav podem aparecer em todos, inclusive no menu fav

                    // Itens ocultos APENAS aparecem no menu HIDE (nunca em outros menus, nem FAV)
                    if (isHidden)
                    {
                        // Adicionar APENAS ao menu HIDE
                        tagScrollViewElements["HIDE"].Add(repoButton.repoScrollViewElement);
                    }
                    else
                    {
                        // Item não está oculto, adicionar aos menus normais
                        tagScrollViewElements["ALL"].Add(repoButton.repoScrollViewElement);

                        // Processar tags de membros (LIMBS)
                        if (LIMB_TAGS.Contains(parentTag.ToUpper()))
                        {
                            tagScrollViewElements["LIMBS"].Add(repoButton.repoScrollViewElement);
                        }
                        // Adicionar à tag pai
                        else if (tagScrollViewElements.TryGetValue(parentTag.ToUpper(), out var elements))
                        {
                            elements.Add(repoButton.repoScrollViewElement);
                        }

                        // Adicionar ao menu FAV se for favorito (mas só se NÃO estiver oculto)
                        if (isFavorite)
                        {
                            tagScrollViewElements["FAV"].Add(repoButton.repoScrollViewElement);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建装饰物按钮时出错: {e.Message}");
            }
        }

        // 添加操作按钮（关闭、清除所有）
        private static void AddActionButtons(REPOPopupPage page)
        {
            try
            {
                // 创建关闭按钮 - 放在页面底部，不在滚动区域内
                page.AddElement(parent =>
                {
                    MenuAPI.CreateREPOButton(
                        "<size=18><color=#FFFFFF>C</color><color=#E6E6E6>L</color><color=#CCCCCC>O</color><color=#B3B3B3>S</color><color=#999999>E</color></size>",
                        OnCloseButtonClick,
                        parent,
                        new Vector2(301, 0)
                    );
                });

                // 创建"关闭所有模型"按钮 - 放在关闭按钮旁边，使用橙黄色底色和黑色文字
                page.AddElement(parent =>
                {
                    MenuAPI.CreateREPOButton(
                        "<size=18><color=#FFAA00>CLEAR ALL</color></size>",
                        OnDisableAllButtonClick,
                        parent,
                        new Vector2(401, 0)
                    );
                });
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建操作按钮时出错: {e.Message}");
            }
        }

        // 显示指定标签的装饰物
        private static void ShowTagDecorations(string tag)
        {
            try
            {
                // 页面检查
                if (decorationsPage == null)
                    return;

                ReorderAllButtonsBySorting();

                // 隐藏当前标签的装饰物按钮
                if (!string.IsNullOrEmpty(currentTagFilter) &&
                    tagScrollViewElements.TryGetValue(currentTagFilter, out var currentElements))
                {
                    foreach (var element in currentElements)
                    {
                        if (element != null)
                        {
                            element.visibility = false;
                        }
                    }
                }

                // 显示新标签的装饰物按钮
                if (!string.IsNullOrEmpty(tag) &&
                    tagScrollViewElements.TryGetValue(tag, out var newElements))
                {
                    foreach (var element in newElements)
                    {
                        if (element != null)
                        {
                            element.visibility = true;
                        }
                    }
                }

                // 更新当前标签
                currentTagFilter = tag;

                // 更新滚动视图
                decorationsPage.scrollView.SetScrollPosition(0);
                decorationsPage.scrollView.UpdateElements();
            }
            catch (Exception e)
            {
                Logger?.LogError($"显示标签 {tag} 的装饰物时出错: {e.Message}");
            }
        }

        private static void ShowTagDecorations2(string tag)
        {
            try
            {
                // 页面检查
                if (decorationsPage == null)
                    return;

                ReorderAllButtonsBySorting();

                foreach (var btn in decorationButtons.Values)
                {
                    if (btn?.repoScrollViewElement != null)
                    {
                        btn.repoScrollViewElement.visibility = false;
                    }
                }

                if (!string.IsNullOrEmpty(tag) &&
                    tagScrollViewElements.TryGetValue(tag, out var newElements))
                {
                    foreach (var element in newElements)
                    {
                        if (element != null)
                        {
                            element.visibility = true;
                        }
                    }
                }

                // 更新当前标签
                currentTagFilter = tag;

                // 更新滚动视图
                decorationsPage.scrollView.SetScrollPosition(0);
                decorationsPage.scrollView.UpdateElements();
            }
            catch (Exception e)
            {
                Logger?.LogError($"显示标签 {tag} 的装饰物时出错: {e.Message}");
            }
        }

        // 延迟显示标签装饰物
        private static System.Collections.IEnumerator DelayedShowTagDecorations(string tag)
        {
            // 等待一帧
            yield return null;

            try
            {
                ShowTagDecorations(tag);
            }
            catch (Exception e)
            {
                Logger?.LogError($"延迟显示标签装饰物时出错: {e.Message}");
            }
        }

        // 创建装备方案切换按钮
        private static void CreateOutfitButtons(REPOPopupPage? page)
        {
            try
            {
                // 清空装备方案按钮字典
                outfitButtons.Clear();

                // 获取当前选中的方案索引
                int currentOutfit = ConfigManager.GetCurrentOutfitIndex();

                // 按钮的垂直间距
                const int buttonSpacing = 25;
                // 固定X坐标
                const int x = 640;
                // 起始Y坐标
                const int startY = 260;

                // 为每个装备方案创建按钮
                for (int i = 1; i <= 9; i++)
                {
                    int outfitIndex = i; // 捕获循环变量

                    // 添加快捷键信息到按钮文本
                    string shortcutInfo = $"<size=10><color=#888888>[F{outfitIndex}]</color></size>";

                    string label = GetOutfitLabel(outfitIndex);

                    // 如果是当前选中的方案，则使用加粗效果
                    string buttonText = outfitIndex == currentOutfit ?
                        $"<size=14><color=#FFCC00><b>{label}</b></color></size> {shortcutInfo}" :
                        $"<size=14><color=#CCCCCC>{label}</color></size> {shortcutInfo}";

                    // 计算按钮位置 - 垂直排列，从上到下
                    int yPosition = startY - (i - 1) * buttonSpacing;

                    page?.AddElement(parent =>
                    {
                        var button = MenuAPI.CreateREPOButton(
                            buttonText,
                            () => OnOutfitButtonClick(outfitIndex),
                            parent,
                            new Vector2(x, yPosition)
                        );

                        // 添加到装备方案按钮字典
                        outfitButtons[outfitIndex] = button;
                    });
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建装备方案按钮时出错: {e.Message}");
            }
        }

        // 获取装备方案显示名称（优先使用自定义配置，空则回退到数字）
        private static string GetOutfitLabel(int index)
        {
            string? label = index switch
            {
                1 => outfitLabel1?.Value,
                2 => outfitLabel2?.Value,
                3 => outfitLabel3?.Value,
                4 => outfitLabel4?.Value,
                5 => outfitLabel5?.Value,
                6 => outfitLabel6?.Value,
                7 => outfitLabel7?.Value,
                8 => outfitLabel8?.Value,
                9 => outfitLabel9?.Value,
                _ => null
            };
            return string.IsNullOrWhiteSpace(label) ? index.ToString() : label!;
        }

        private static void CreateOutfitInputFields(REPOPopupPage? page)
        {
            try
            {
                const int buttonSpacing = 25;
                const int x = 540;
                const int startY = 260;

                for (int i = 1; i <= 9; i++)
                {
                    int outfitIndex = i;

                    int yPosition = startY - (i - 1) * buttonSpacing;

                    page?.AddElement(parent =>
                    {
                        var inputField = MenuAPI.CreateREPOInputField(
                            $"F{outfitIndex}",
                            (value) =>
                            {
                                string cleanValue = (value ?? "")
                                    .Replace("<b>|</b>", "")
                                    .Trim();

                                switch (outfitIndex)
                                {
                                    case 1:
                                        if (outfitLabel1 != null) outfitLabel1.Value = cleanValue;
                                        break;
                                    case 2:
                                        if (outfitLabel2 != null) outfitLabel2.Value = cleanValue;
                                        break;
                                    case 3:
                                        if (outfitLabel3 != null) outfitLabel3.Value = cleanValue;
                                        break;
                                    case 4:
                                        if (outfitLabel4 != null) outfitLabel4.Value = cleanValue;
                                        break;
                                    case 5:
                                        if (outfitLabel5 != null) outfitLabel5.Value = cleanValue;
                                        break;
                                    case 6:
                                        if (outfitLabel6 != null) outfitLabel6.Value = cleanValue;
                                        break;
                                    case 7:
                                        if (outfitLabel7 != null) outfitLabel7.Value = cleanValue;
                                        break;
                                    case 8:
                                        if (outfitLabel8 != null) outfitLabel8.Value = cleanValue;
                                        break;
                                    case 9:
                                        if (outfitLabel9 != null) outfitLabel9.Value = cleanValue;
                                        break;
                                }

                                UpdateOutfitButtonHighlights();

                                ConfigManager.SaveConfig();
                            },
                            parent,
                            new Vector2(x, yPosition),
                            true,
                            $"{outfitIndex}",
                            GetOutfitLabel(outfitIndex) == outfitIndex.ToString() ? "" : GetOutfitLabel(outfitIndex)
                        );

                        float novaLarguraScale = 0.4f;
                        float moverFundoEsquerda = -45f;

                        var sliderBG = inputField.transform.Find("SliderBG");
                        var rawImage = inputField.transform.Find("RawImage");
                        var rawImage1 = inputField.transform.Find("RawImage (1)");

                        if (sliderBG != null)
                        {
                            sliderBG.localScale = new Vector3(novaLarguraScale, sliderBG.localScale.y, sliderBG.localScale.z);
                            sliderBG.localPosition = new Vector3(
                                sliderBG.localPosition.x + moverFundoEsquerda,
                                sliderBG.localPosition.y,
                                sliderBG.localPosition.z
                            );
                        }

                        if (rawImage != null)
                        {
                            rawImage.localScale = new Vector3(novaLarguraScale, rawImage.localScale.y, rawImage.localScale.z);
                            rawImage.localPosition = new Vector3(
                                rawImage.localPosition.x + moverFundoEsquerda,
                                rawImage.localPosition.y,
                                rawImage.localPosition.z
                            );
                        }

                        if (rawImage1 != null)
                        {
                            rawImage1.localScale = new Vector3(novaLarguraScale, rawImage1.localScale.y, rawImage1.localScale.z);
                            rawImage1.localPosition = new Vector3(
                                rawImage1.localPosition.x + moverFundoEsquerda,
                                rawImage1.localPosition.y,
                                rawImage1.localPosition.z
                            );
                        }

                        float larguraPixels = novaLarguraScale * 100f;
                        inputField.inputStringSystem.maskRectTransform.sizeDelta = new Vector2(larguraPixels - 3.5f, 20f);

                        float scaleDiff = 70f;

                        var labelPos = inputField.labelTMP.rectTransform.localPosition;
                        inputField.labelTMP.rectTransform.localPosition = new Vector3(
                            labelPos.x + scaleDiff,
                            labelPos.y,
                            labelPos.z
                        );

                        outfitInputFields[outfitIndex] = inputField;

                        if (inputField?.gameObject != null)
                        {
                            inputField.gameObject.SetActive(false);
                        }

                        if (inputField?.inputStringSystem != null)
                        {
                            var originalOnValueChanged = inputField.inputStringSystem.onValueChanged;
                            inputField.inputStringSystem.onValueChanged = (value) =>
                            {
                                originalOnValueChanged?.Invoke(value);

                                if (inputField?.gameObject != null)
                                {
                                    inputField.gameObject.SetActive(false);
                                }
                                if (outfitButtons.TryGetValue(outfitIndex, out var btn) && btn?.gameObject != null)
                                {
                                    btn.gameObject.SetActive(true);
                                }
                            };
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"创建装备方案标签输入框时出错: {e.Message}");
            }
        }

        // 装备方案按钮点击事件
        private static void OnOutfitButtonClick(int outfitIndex)
        {
            try
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                if (isCtrlPressed)
                {
                    if (outfitButtons.TryGetValue(outfitIndex, out var btn) && btn?.gameObject != null)
                    {
                        btn.gameObject.SetActive(false);
                    }

                    if (outfitInputFields.TryGetValue(outfitIndex, out var input) && input?.gameObject != null)
                    {
                        input.gameObject.SetActive(true);

                        string currentLabel = GetOutfitLabel(outfitIndex);
                        if (currentLabel == outfitIndex.ToString())
                        {
                            currentLabel = "";
                        }
                        input.inputStringSystem?.SetValue(currentLabel, false);

                        if (input.inputStringSystem != null)
                        {
                            input.inputStringSystem.isFocused = true;
                        }
                    }

                    return;
                }

                // 获取当前选中的方案索引
                int currentOutfit = ConfigManager.GetCurrentOutfitIndex();

                // 如果点击的是当前方案，不做切换操作
                if (outfitIndex == currentOutfit)
                {
                    return;
                }

                // 切换到新的装备方案
                ConfigManager.SwitchOutfit(outfitIndex);

                // 更新装备方案按钮高亮状态
                UpdateOutfitButtonHighlights();

                // 更新装饰物状态
                UpdateDecorations();

                // 更新按钮状态
                UpdateButtonStates();
            }
            catch (Exception e)
            {
                Logger?.LogError($"切换装备方案时出错: {e.Message}");
            }
        }

        // 更新装备方案按钮高亮状态
        private static void UpdateOutfitButtonHighlights()
        {
            try
            {
                // 获取当前选中的方案索引
                int currentOutfit = ConfigManager.GetCurrentOutfitIndex();

                // 更新每个装备方案按钮的高亮状态
                for (int i = 1; i <= 9; i++)
                {
                    if (outfitButtons.TryGetValue(i, out REPOButton button))
                    {
                        // 添加快捷键信息到按钮文本
                        string shortcutInfo = $"<size=10><color=#888888>[F{i}]</color></size>";
                        string label = GetOutfitLabel(i);

                        // 如果是当前选中的方案，则使用加粗效果
                        string buttonText = i == currentOutfit ?
                            $"<size=14><color=#FFCC00><b>{label}</b></color></size> {shortcutInfo}" :
                            $"<size=14><color=#CCCCCC>{label}</color></size> {shortcutInfo}";

                        // atalhos F1-F9 para mudar o outfit
                        button.labelTMP.text = buttonText;

                        ReorderAllButtonsBySorting();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"更新装备方案按钮高亮状态时出错: {e.Message}");
            }
        }

        private static void CreateDecorationSearchField(REPOPopupPage? page)
        {
            try
            {
                page?.AddElement(parent =>
                {
                    decorationSearchField = MenuAPI.CreateREPOInputField(
                        "Search item:",
                        (value) =>
                        {
                            OnSearchTextChanged(value ?? "");
                        },
                        parent,
                        new Vector2(70, 310)
                    );
                });

                page?.AddElement(parent =>
                {
                    searchClearButton = MenuAPI.CreateREPOButton(
                        "<size=13><color=#FF4444>[X]</color></size>",
                        OnSearchClearButtonClick,
                        parent,
                        new Vector2(323, 313)
                    );

                    if (searchClearButton?.gameObject != null)
                    {
                        searchClearButton.gameObject.SetActive(false);
                    }
                });
            }
            catch (Exception e)
            {
                Logger?.LogError($"✗ Erro ao criar campo de busca: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void OnSearchTextChanged(string searchText)
        {
            try
            {
                string originalText = searchText ?? "";
                searchText = new string(originalText.Where(c => c >= 32 && c != 127).ToArray());

                if (searchText != originalText && decorationSearchField?.inputStringSystem != null)
                {
                    decorationSearchField.inputStringSystem.SetValue(searchText, false);
                }

                currentSearchText = searchText.Trim();

                if (searchClearButton?.gameObject != null)
                {
                    searchClearButton.gameObject.SetActive(!string.IsNullOrEmpty(currentSearchText));
                }

                if (decorationsPage == null)
                {
                    Logger?.LogWarning("⚠️ Página de decorações é nula, não é possível atualizar");
                    return;
                }

                if (string.IsNullOrEmpty(currentSearchText))
                {
                    isSearching = false;
                    searchResults.Clear();
                    ShowTagDecorations2(currentTagFilter);
                    return;
                }

                isSearching = true;
                searchResults.Clear();

                List<DecorationInfo> decorationsToSearch;

                if (decorationDataCache.TryGetValue(currentTagFilter, out var tagDecorations))
                {
                    decorationsToSearch = tagDecorations;
                }
                else
                {
                    decorationsToSearch = HeadDecorationManager.Decorations.ToList();
                }

                searchResults = decorationsToSearch.Where(d =>
                    (d.Name != null && d.Name.IndexOf(currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (d.DisplayName != null && d.DisplayName.IndexOf(currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();

                foreach (var button in decorationButtons.Values)
                {
                    button.repoScrollViewElement.visibility = false;
                }

                int visibleCount = 0;
                foreach (var decoration in searchResults)
                {
                    if (decorationButtons.TryGetValue(decoration.Name ?? string.Empty, out var button))
                    {
                        button.repoScrollViewElement.visibility = true;
                        visibleCount++;
                    }
                    else
                    {
                        Logger?.LogWarning($"⚠️ Botão não encontrado para: {decoration.DisplayName}");
                    }
                }

                decorationsPage.scrollView.SetScrollPosition(0);
                decorationsPage.scrollView.UpdateElements();
            }
            catch (Exception e)
            {
                Logger?.LogError($"✗ Erro ao processar mudança de texto de busca: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void OnSearchClearButtonClick()
        {
            try
            {
                ClearSearchField();
                ShowTagDecorations2(currentTagFilter);
            }
            catch (Exception e)
            {
                Logger?.LogError($"✗ Erro ao clicar no botão de limpar busca: {e.Message}");
            }
        }

        private static void ClearSearchField()
        {
            try
            {
                if (decorationSearchField?.inputStringSystem != null)
                {
                    decorationSearchField.inputStringSystem.SetValue("", false);
                    currentSearchText = "";
                    isSearching = false;
                    searchResults.Clear();
                }

                if (searchClearButton?.gameObject != null)
                {
                    searchClearButton.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"✗ Erro ao limpar campo de busca: {e.Message}");
            }
        }

        // 检测快捷键组件
        private class ShortcutKeyListener : MonoBehaviour
        {
            // 静态单例实例
            public static ShortcutKeyListener? Instance { get; private set; }

            private void Awake()
            {
                // 单例模式实现
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            private void OnDestroy()
            {
                if (Instance == this)
                {
                    Instance = null;
                }
            }

            private void Update()
            {
                // 检测F1至F9功能键
                for (int i = 1; i <= 9; i++)
                {
                    // 根据索引确定功能键
                    KeyCode fKey = (KeyCode)((int)KeyCode.F1 + (i - 1));

                    if (Input.GetKeyDown(fKey))
                    {
                        // 调用切换装备方案函数
                        SwitchOutfitWithShortcut(i);
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {
                    CheckInputFieldsClickOutside();
                }
            }
        }

        private static void CheckInputFieldsClickOutside()
        {
            try
            {
                foreach (var kvp in outfitInputFields)
                {
                    int outfitIndex = kvp.Key;
                    var inputField = kvp.Value;

                    if (inputField?.gameObject != null && inputField.gameObject.activeSelf)
                    {
                        // Se o input está ativo mas não está focado (clicou fora)
                        if (inputField.inputStringSystem != null && !inputField.inputStringSystem.isHovering)
                        {
                            // Pega o valor atual do input antes de fechar (do TextMeshPro)
                            string rawValue = inputField.inputStringSystem.inputTMP?.text ?? "";

                            // Remove o cursor "<b>|</b>" e espaços extras
                            string cleanValue = rawValue
                                .Replace("<b>|</b>", "")
                                .Trim();

                            // Salva o valor no ConfigEntry
                            switch (outfitIndex)
                            {
                                case 1:
                                    if (outfitLabel1 != null) outfitLabel1.Value = cleanValue;
                                    break;
                                case 2:
                                    if (outfitLabel2 != null) outfitLabel2.Value = cleanValue;
                                    break;
                                case 3:
                                    if (outfitLabel3 != null) outfitLabel3.Value = cleanValue;
                                    break;
                                case 4:
                                    if (outfitLabel4 != null) outfitLabel4.Value = cleanValue;
                                    break;
                                case 5:
                                    if (outfitLabel5 != null) outfitLabel5.Value = cleanValue;
                                    break;
                                case 6:
                                    if (outfitLabel6 != null) outfitLabel6.Value = cleanValue;
                                    break;
                                case 7:
                                    if (outfitLabel7 != null) outfitLabel7.Value = cleanValue;
                                    break;
                                case 8:
                                    if (outfitLabel8 != null) outfitLabel8.Value = cleanValue;
                                    break;
                                case 9:
                                    if (outfitLabel9 != null) outfitLabel9.Value = cleanValue;
                                    break;
                            }

                            // Atualiza os botões e salva config
                            UpdateOutfitButtonHighlights();
                            ConfigManager.SaveConfig();

                            // Fecha o input e volta ao botão
                            inputField.gameObject.SetActive(false);

                            if (outfitButtons.TryGetValue(outfitIndex, out var btn) && btn?.gameObject != null)
                            {
                                btn.gameObject.SetActive(true);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogWarning($"Erro ao verificar clique fora dos inputs: {e.Message}");
            }
        }

        // 通过快捷键切换装备方案
        private static void SwitchOutfitWithShortcut(int outfitIndex)
        {
            try
            {
                // 如果当前已经是该方案，不做切换
                int currentOutfit = ConfigManager.GetCurrentOutfitIndex();
                if (outfitIndex == currentOutfit)
                    return;

                // 切换到新的装备方案
                ConfigManager.SwitchOutfit(outfitIndex);

                // 如果MoreHead页面已打开，更新UI显示
                if (decorationsPage != null && decorationsPage.menuPage.isActiveAndEnabled)
                {
                    // 更新装备方案按钮高亮状态
                    UpdateOutfitButtonHighlights();

                    // 更新按钮状态
                    UpdateButtonStates();
                }

                // 更新所有装饰物状态 - 不论页面是否打开都执行
                UpdateDecorations();
            }
            catch (Exception e)
            {
                Logger?.LogError($"通过快捷键切换装备方案时出错: {e.Message}");
            }
        }

        // 创建按键监听器实例
        public static void InitializeShortcutListener()
        {
            try
            {
                // 检查单例是否已存在
                if (ShortcutKeyListener.Instance == null)
                {
                    // 创建持久性游戏对象
                    GameObject listenerObject = new GameObject("MoreHeadShortcutListener");
                    listenerObject.AddComponent<ShortcutKeyListener>();
                    Logger?.LogInfo("已初始化装备方案快捷键监听器");
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"初始化快捷键监听器时出错: {e.Message}");
            }
        }
    }
}
