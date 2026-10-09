using HarmonyLib;
using AmongUs.GameOptions;
using UnityEngine;
using UnityEngine.Events;
namespace Forger;

internal static class NativeRoles
{
    static RolesSettingsMenu? menu;
    static readonly Dictionary<IntPtr,(Transform transform,Vector3 position)> nativeTabPositions=new();
    static float tabSpacing;
    static float tabMin,tabMax;
    static readonly Dictionary<IntPtr,CustomRole> quota=new();
    static readonly List<(RoleOptionSetting row,CustomRole role)> rows=new();
    static readonly List<(NumberOption row,RoleSetting setting)> numbers=new();
    static readonly List<(ToggleOption row,RoleSetting setting)> toggles=new();
    static readonly Dictionary<string,Sprite> icons=new();
    internal static Sprite Icon(CustomRole role)
    {
        if(icons.TryGetValue(role.Name,out var cached))return cached;
        if(role.Icon=="native:impostor")return RoleManager.Instance.GetRole(RoleTypes.Impostor).RoleIconWhite;
        if(role.Icon=="native:scientist")return RoleManager.Instance.GetRole(RoleTypes.Scientist).RoleIconWhite;
        if(role.Icon=="native:engineer")return RoleManager.Instance.GetRole(RoleTypes.Engineer).RoleIconWhite;
        if(role.Icon=="native:guardian")return RoleManager.Instance.GetRole(RoleTypes.GuardianAngel).RoleIconWhite;
        using var s=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets."+role.Icon)!;using var b=new MemoryStream();s.CopyTo(b);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,b.ToArray(),false);texture.filterMode=FilterMode.Point;
        return icons[role.Name]=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);
    }
    static void Text(TMPro.TextMeshPro text,string value) {foreach(var t in text.GetComponents<TextTranslatorTMP>())t.enabled=false;text.text=value;}
    static void Click(PassiveButton button,Action action) {button.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.OnClick.AddListener((UnityAction)(()=>{if(Game.Host)action();}));}
    internal static void Tabs(RolesSettingsMenu m)
    {
        menu=m;nativeTabPositions.Clear();
        var xs=m.roleTabs.ToArray().Where(t=>t).Select(t=>t.transform.localPosition.x).Distinct().OrderBy(x=>x).ToArray();
        tabSpacing=xs.Length>1?xs[1]-xs[0]:.8f;
        tabMin=m.roleSettingsTabScroller.ContentXBounds.min;tabMax=m.roleSettingsTabScroller.ContentXBounds.max;
        Plugin.Instance.Log.LogInfo("Native registered role tabs="+m.roleTabs.Count);
    }
    internal static void Quota(RolesSettingsMenu m)
    {
        menu=m;rows.Clear();quota.Clear();numbers.Clear();
        foreach(var row in m.roleChances)
        {
            if(!row || !row.role)continue;var role=RegisteredRoles.Get(row.role.Role);if(role==null)continue;
            row.name="CustomRole_"+role.Name;quota[row.Pointer]=role;rows.Add((row,role));
            if(role.Alignment==RoleAlignment.Neutral)row.labelSprite.color=Cannibal.Color;
            Click(row.CountMinusBtn,()=>{role.Count.Value=0;CustomRoles.Changed();});Click(row.CountPlusBtn,()=>{role.Count.Value=1;CustomRoles.Changed();});
            Click(row.ChanceMinusBtn,()=>{role.Chance.Value=Math.Max(0,role.Chance.Value-10);CustomRoles.Changed();});Click(row.ChancePlusBtn,()=>{role.Chance.Value=Math.Min(100,role.Chance.Value+10);CustomRoles.Changed();});
        }
        foreach(var label in m.RoleChancesSettings.GetComponentsInChildren<TMPro.TextMeshPro>(true))if(label.text=="Crewmate Roles")Text(label,"Crew / Neutral Roles");
        Refresh();
    }
    internal static void Open(RolesSettingsMenu m,CustomRole role,PassiveButton tab)
    {
        m.RoleChancesSettings.SetActive(false);m.AdvancedRolesSettings.SetActive(true);
        foreach(var option in m.advancedSettingChildren)if(option)UnityEngine.Object.Destroy(option.gameObject);m.advancedSettingChildren.Clear();numbers.Clear();
        m.currentTabButton=tab;Text(m.roleTitleText,role.Name);Text(m.roleDescriptionText,role.Description);Text(m.roleHeaderText,role.Name);
        if(role.Alignment==RoleAlignment.Neutral)m.roleHeaderSprite.color=Cannibal.Color;
        m.roleScreenshot.sprite=Icon(role);m.roleScreenshot.color=Color.white;
        var options=role.Settings;
        toggles.Clear();
        for(int i=0;i<options.Length;i++)
        {
            var setting=options[i];var row=UnityEngine.Object.Instantiate(m.numberOptionOrigin,m.AdvancedRolesSettings.transform);row.name="CustomSetting_"+role.Name+"_"+i;row.enabled=false;
            if(setting.Toggle)
            {
                UnityEngine.Object.Destroy(row.gameObject);
                var toggle=UnityEngine.Object.Instantiate(m.checkboxOrigin,m.AdvancedRolesSettings.transform);toggle.name="CustomToggle_"+role.Name+"_"+i;toggle.enabled=false;
                toggle.transform.localPosition=new Vector3(RolesSettingsMenu.X_ADVANCED_START,RolesSettingsMenu.Y_ADVANCED_START+i*RolesSettingsMenu.Y_ADVANCED_OFFSET,toggle.transform.localPosition.z);
                var toggleData=ScriptableObject.CreateInstance<CheckboxGameSetting>();toggleData.Type=OptionTypes.Checkbox;toggleData.OptionName=BoolOptionNames.ConfirmImpostor;
                toggle.SetUpFromData(toggleData,RolesSettingsMenu.MASK_LAYER);Text(toggle.TitleText,setting.Label);toggle.SetClickMask(m.ButtonClickMask);
                var passive=toggle.GetComponent<PassiveButton>();Click(passive,()=>{setting.Set(setting.Get()>=.5f?0:1);CustomRoles.Changed();});
                toggles.Add((toggle,setting));m.advancedSettingChildren.Add(toggle);toggle.gameObject.SetActive(true);continue;
            }
            row.transform.localPosition=new Vector3(RolesSettingsMenu.X_ADVANCED_START,RolesSettingsMenu.Y_ADVANCED_START+i*RolesSettingsMenu.Y_ADVANCED_OFFSET,row.transform.localPosition.z);
            var data=ScriptableObject.CreateInstance<FloatGameSetting>();data.Type=OptionTypes.Float;data.OptionName=FloatOptionNames.KillCooldown;data.Value=setting.Get();data.ValidRange=new FloatRange(setting.Min,setting.Max);data.Increment=setting.Step;data.FormatString="0";
            row.SetUpFromData(data,RolesSettingsMenu.MASK_LAYER);Text(row.TitleText,setting.Label);row.SetClickMask(m.ButtonClickMask);
            Click(row.MinusBtn,()=>{setting.Set(Math.Max(setting.Min,setting.Get()-setting.Step));CustomRoles.Changed();});Click(row.PlusBtn,()=>{setting.Set(Math.Min(setting.Max,setting.Get()+setting.Step));CustomRoles.Changed();});
            numbers.Add((row,setting));m.advancedSettingChildren.Add(row);row.gameObject.SetActive(true);
        }
        m.scrollBar.ScrollToTop();Refresh();
        Plugin.Instance.Log.LogInfo("Custom advanced rows: "+role.Name+"; y="+RolesSettingsMenu.Y_ADVANCED_START+"; step="+RolesSettingsMenu.Y_ADVANCED_OFFSET+"; parent="+m.AdvancedRolesSettings.name);

    }
    internal static void Layout()
    {
        if(menu is null || !menu || !menu.gameObject.activeInHierarchy)return;
        var horizontal=menu.roleSettingsTabScroller;
        var last=menu.roleTabs.ToArray().Where(t=>t).OrderBy(t=>t.transform.position.x).LastOrDefault();
        if(last && horizontal.Hitbox)
        {
            var right=last!.GetComponentInChildren<SpriteRenderer>().bounds.max.x;
            tabMin=Math.Min(0,horizontal.Inner.localPosition.x+(horizontal.Hitbox.bounds.max.x-right-.05f)/Math.Abs(horizontal.Inner.lossyScale.x));tabMax=0;
        }
        var tabBounds=menu.roleSettingsTabScroller.ContentXBounds;tabBounds.min=tabMin;tabBounds.max=tabMax;menu.roleSettingsTabScroller.ContentXBounds=tabBounds;
        var camera=HudManager.Instance?HudManager.Instance.UICamera:Camera.main;
        if(camera && menu.roleSettingsTabScroller.Hitbox)
        {
            var area=menu.roleSettingsTabScroller.Hitbox.bounds;
            var a=camera.WorldToScreenPoint(area.min);var b=camera.WorldToScreenPoint(area.max);var mouse=Input.mousePosition;
            if(mouse.x>=Math.Min(a.x,b.x) && mouse.x<=Math.Max(a.x,b.x) && mouse.y>=Math.Min(a.y,b.y) && mouse.y<=Math.Max(a.y,b.y))
            {
                var wheel=Input.mouseScrollDelta;var delta=wheel.y!=0?wheel.y:-wheel.x;
                if(delta!=0){var inner=menu.roleSettingsTabScroller.Inner;var position=inner.localPosition;position.x=Math.Clamp(position.x+delta*tabSpacing,tabMin,tabMax);inner.localPosition=position;}
            }
        }
        var renders=new List<SpriteRenderer>();
        if(menu.RoleChancesSettings.activeInHierarchy){foreach(var (row,role) in rows)if(row&&row.gameObject.activeInHierarchy)renders.Add(row.labelSprite);}
        else if(menu.AdvancedRolesSettings.activeInHierarchy){foreach(var option in menu.advancedSettingChildren)if(option&&option.gameObject.activeInHierarchy&&option.name.StartsWith("Custom"))renders.Add(option.TryCast<RoleOptionSetting>()?.labelSprite ?? option.LabelBackground);}
        renders=renders.Where(r=>r).ToList();if(renders.Count==0)return;
        var bottom=renders.Where(r=>r).Min(r=>r.bounds.min.y);var scroll=menu.scrollBar;
        var required=scroll.Inner.localPosition.y+(menu.MaskArea.bounds.min.y+.1f-bottom)/Math.Abs(scroll.Inner.lossyScale.y);
        scroll.SetYBoundsMax(Math.Max(0,required));scroll.UpdateScrollBars();
    }
    
    
    internal static bool Own(RoleOptionSetting row) => quota.ContainsKey(row.Pointer) && row.name.StartsWith("Custom");
    internal static void Refresh()
    {
        foreach(var (row,setting) in toggles)if(row){row.oldValue=setting.Get()>=.5f;row.CheckMark.enabled=row.oldValue;}
        foreach(var (row,role) in rows) if(row)
        {
            row.roleMaxCount=role.Count.Value;row.roleChance=role.Chance.Value;Text(row.countText,role.Count.Value.ToString());Text(row.chanceText,role.Chance.Value+"%");
            row.CountMinusBtn.SetInteractable(Game.Host&&role.Count.Value>0);row.CountPlusBtn.SetInteractable(Game.Host&&role.Count.Value<1);row.ChanceMinusBtn.SetInteractable(Game.Host&&role.Chance.Value>0);row.ChancePlusBtn.SetInteractable(Game.Host&&role.Chance.Value<100);
        }
        foreach(var (row,setting) in numbers)if(row){row.Value=setting.Get();row.oldValue=row.Value;Text(row.ValueText,row.Value.ToString("0")+setting.Unit);row.MinusBtn.SetInteractable(Game.Host&&row.Value>setting.Min);row.PlusBtn.SetInteractable(Game.Host&&row.Value<setting.Max);}
    }
}
[HarmonyPatch(typeof(RolesSettingsMenu),nameof(RolesSettingsMenu.InitialSetup))]
internal static class CustomRoleTabsPatch {static void Prefix(){RegisteredRoles.Ensure(RoleManager.Instance);} static void Postfix(RolesSettingsMenu __instance){try{NativeRoles.Tabs(__instance);}catch(Exception e){Plugin.Instance.Log.LogError("Custom tabs: "+e);}}}
[HarmonyPatch(typeof(RolesSettingsMenu),nameof(RolesSettingsMenu.SetQuotaTab))]
internal static class CustomRoleQuotaPatch {static void Postfix(RolesSettingsMenu __instance){try{NativeRoles.Quota(__instance);}catch(Exception e){Plugin.Instance.Log.LogError("Custom quota: "+e);}}}
[HarmonyPatch(typeof(RoleOptionSetting),nameof(RoleOptionSetting.UpdateValuesAndText))]
internal static class CustomRoleRefreshPatch {static bool Prefix(RoleOptionSetting __instance){if(!NativeRoles.Own(__instance))return true;NativeRoles.Refresh();return false;}}
public sealed class CustomRolesUI:MonoBehaviour
{
    public CustomRolesUI(IntPtr p):base(p){}
    void LateUpdate(){RoleIntro.Refresh();RoleNamePrivacy.Refresh();}
    void Update(){CustomRoles.Sync();NativeRoles.Layout();RoleIntro.Refresh();}
}
[HarmonyPatch(typeof(RolesSettingsMenu),nameof(RolesSettingsMenu.OpenMenu))]
internal static class CustomRoleOpenPatch {static void Postfix(RolesSettingsMenu __instance){try{NativeRoles.Tabs(__instance);}catch(Exception e){Plugin.Instance.Log.LogError("Custom tabs open: "+e);}}}



[HarmonyPatch(typeof(RolesSettingsMenu),nameof(RolesSettingsMenu.ChangeTab))]
internal static class NativeCustomAdvancedTab
{
    static bool Prefix(RolesSettingsMenu __instance,RoleBehaviour role,PassiveButton button)
    {
        var custom=RegisteredRoles.Get(role.Role);if(custom==null)return true;
        // Let the native menu initialize its impostor layout, then populate our settings.
        __instance.ChangeTab(RoleManager.Instance.GetRole(custom.Alignment==RoleAlignment.Impostor?RoleTypes.Shapeshifter:RoleTypes.Scientist),button);
        NativeRoles.Open(__instance,custom,button);return false;
    }
}


[HarmonyPatch(typeof(RoleSettingsTabButton),nameof(RoleSettingsTabButton.SetButton))]
internal static class NativeCustomTabIcon
{
    static readonly Dictionary<IntPtr,Vector3> scales=new();
    static void Postfix(RoleSettingsTabButton __instance,RoleBehaviour role)
    {
        if(RegisteredRoles.Get(role.Role)==null || !__instance.icon || !__instance.icon.sprite)return;
        var descriptor=RegisteredRoles.Get(role.Role)!;
        if(descriptor.Alignment==RoleAlignment.Neutral)__instance.background.color=Cannibal.Color;
        var baseline=RoleManager.Instance.GetRole(descriptor.Alignment==RoleAlignment.Impostor?RoleTypes.Shapeshifter:RoleTypes.Scientist).RoleIconWhite;
        if(!scales.TryGetValue(__instance.Pointer,out var scale)){scale=__instance.icon.transform.localScale;scales[__instance.Pointer]=scale;}
        if(baseline)__instance.icon.transform.localScale=scale*(baseline.bounds.size.x/__instance.icon.sprite.bounds.size.x);
    }
}



