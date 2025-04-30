using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using AncientChineseMythology.Content;
using AncientChineseMythology.Players;
using AncientChineseMythology.UI.Elements;
using ReLogic.Content;

namespace AncientChineseMythology.UI;

public class MythologySidebar : UIState
{
    /* ── 参数 ───────────────────────────── */
    private const int W = 220;          // 面板宽
    private const int H = 220;          // 面板高
    private const int TAB = 32;         // 标签宽高

    private bool _collapsed = true;

    private SidebarTabButton _tab;
    private UIPanel _panel;
    private UIText _header, _hp, _mana, _def, _luck, _realm, _exp;
    private UIExpBar _bar;

    private MythologyPlayer MP => Main.LocalPlayer.GetModPlayer<MythologyPlayer>();

    public override void OnInitialize()
    {
        Width.Set(W + TAB, 0);
        Left.Set(0, 0);
        Top.Set(270, 0);

        // 主面板
        _panel = new UIPanel();
        _panel.SetPadding(6);
        _panel.Left.Set(-W, 0);
        _panel.Width.Set(W, 0);
        _panel.Height.Set(H, 0);
        Append(_panel);

        // 标签按钮
        var tex = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/ToggleTab");
        _tab = new SidebarTabButton(tex);
        _tab.Left.Set(0, 0);                                 // 与容器同起点
        _tab.Top.Set((H - TAB) / 2f, 0);
        _tab.Width.Set(TAB, 0); _tab.Height.Set(TAB, 0);
        _tab.OnLeftClick += (_, _) => Toggle();
        Append(_tab);

        /* 文字控件 */
        float y = 0;
        _header = Add(ref y);
        _hp     = Add(ref y);
        _mana   = Add(ref y);
        _def    = Add(ref y);
        _luck   = Add(ref y);
        _realm  = Add(ref y);
        y += 2;

        _bar = new UIExpBar();
        _bar.Top.Set(y, 0);
        _bar.Left.Set(0, 0);
        _bar.Width.Set(W - 12, 0);
        _bar.Height.Set(12, 0);
        _panel.Append(_bar);
        y += 18;

        _exp = Add(ref y);

        Toggle();   // 第一次调用让面板展开，玩家进入时就能看到
    }

    private UIText Add(ref float y)
    {
        var t = new UIText("", 0.9f);
        t.Top.Set(y, 0); _panel.Append(t);
        y += 22;
        return t;
    }

    public void Toggle()
    {
        _collapsed = !_collapsed;

        // 仅移动位置，不重建控件，避免事件丢失
        _panel.Left.Pixels = _collapsed ? -W : 0;
        _tab.Left.Pixels   = _collapsed ? 0   : W;
        _tab.IsCollapsed  = _collapsed;          // 让按钮朝向更新

        _panel.Recalculate();
        _tab.Recalculate();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (_collapsed) return;

        var p = Main.LocalPlayer;
        _header.SetText(p.name);
        _hp    .SetText($"❤ {p.statLife}/{p.statLifeMax2}");
        _mana  .SetText($"★ {p.statManaMax2}");
        _def   .SetText($"DEF: {p.statDefense}");

        _realm.SetText($"境界：{CultivationProgression.MajorNames[MP.Major]}·{CultivationProgression.MinorNames[MP.Minor]}");

        int need = CultivationProgression.ExpFor(MP.Major, MP.Minor);
        _bar.SetPercent(need == 0 ? 1f : MP.StageExp / (float)need);

        _exp.SetText(
            $"EXP  {MP.StageExp}/{need}\n" +
            $"Kills {MP.KillsThisMajor}/{CultivationProgression.KillsForMajorUp[MP.Major]}"
        );
    }

    private static int StartExp(int maj,int min)=> min==0?0:CultivationProgression.ExpFor(maj,min-1);
    private static int NeedExp (int maj,int min)=> min>=3?0:CultivationProgression.ExpFor(maj,min);
}

class SidebarTabButton : UIImageButton
{
    public  bool IsCollapsed = true;
    private readonly Asset<Texture2D> Texture2D;
    public SidebarTabButton(Asset<Texture2D> tex) : base(tex) => Texture2D = tex;

    protected override void DrawSelf(SpriteBatch sb)
    {
        var r  = GetDimensions().ToRectangle();
        var fx = IsCollapsed ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        sb.Draw(Texture2D.Value, r, null, Color.White, 0f, Vector2.Zero, fx, 0f);

        if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
    }
}
