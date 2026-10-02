using System.Collections.Generic;
using HeroEscape.Core;

namespace HeroEscape.Unity
{
    /// <summary>Loại thẻ trên tay. Thẻ Hero (Dash) luôn đứng giữa; thẻ phụ trợ xếp hai bên theo thứ tự khai báo.</summary>
    public enum CardKind
    {
        HeroDash,
        SupportStasis,
        SupportBarrier,
    }

    /// <summary>Ảnh chụp một thẻ để hiển thị. Không giữ bộ đếm riêng: mọi giá trị đọc từ GameState và TurnInputController.</summary>
    public readonly struct CardState
    {
        public readonly CardKind Kind;
        public readonly int UsesLeft;
        public readonly int MaxUses;

        /// <summary>Thẻ đang được chọn (chế độ chọn mục tiêu tương ứng đang bật).</summary>
        public readonly bool Selected;

        /// <summary>
        /// Luật hiện tại cho phép kích hoạt thẻ (trận đang diễn ra; Stasis chưa dùng phụ trợ lượt này).
        /// Không tính trạng thái Busy tạm thời của pha địch để thẻ không nhấp nháy mờ/sáng mỗi lượt;
        /// input trong pha địch vẫn bị TurnInputController chặn.
        /// </summary>
        public readonly bool Available;

        public CardState(CardKind kind, int usesLeft, int maxUses, bool selected, bool available)
        {
            Kind = kind;
            UsesLeft = usesLeft;
            MaxUses = maxUses;
            Selected = selected;
            Available = available;
        }

        public bool IsHero => Kind == CardKind.HeroDash;
    }

    /// <summary>
    /// Tay bài suy ra từ trạng thái gameplay. Thẻ có mặt khi level cấp kỹ năng đó và còn ít nhất một lần dùng;
    /// hết lần dùng thì rời tay. Level không cấp gì thì tay rỗng. Thứ tự: Hero trước, rồi phụ trợ.
    /// </summary>
    public static class CardHand
    {
        public static List<CardState> From(TurnInputController input)
        {
            var cards = new List<CardState>(3);
            GameState state = input.Session.Current;
            LevelData level = input.Session.Level;
            bool playing = state.Outcome == GameOutcome.Playing;

            if (level.DashMaxUses > 0 && state.DashUsesLeft > 0)
            {
                cards.Add(new CardState(CardKind.HeroDash, state.DashUsesLeft, level.DashMaxUses,
                    input.Mode == InputMode.ChoosingDashTarget, playing));
            }

            if (level.StasisMaxUses > 0 && state.StasisUsesLeft > 0)
            {
                cards.Add(new CardState(CardKind.SupportStasis, state.StasisUsesLeft, level.StasisMaxUses,
                    input.Mode == InputMode.ChoosingStasisTarget, playing && !state.SupportUsedThisTurn));
            }

            if (level.BarrierMaxUses > 0 && state.BarrierUsesLeft > 0)
            {
                cards.Add(new CardState(CardKind.SupportBarrier, state.BarrierUsesLeft, level.BarrierMaxUses,
                    input.Mode == InputMode.ChoosingBarrierTarget, playing && !state.SupportUsedThisTurn));
            }

            return cards;
        }

        /// <summary>Level có cấp ít nhất một thẻ (dùng để chừa chỗ cho tay bài ở đáy màn hình).</summary>
        public static bool LevelHasCards(LevelData level) => level.DashMaxUses > 0 || level.StasisMaxUses > 0 || level.BarrierMaxUses > 0;

        /// <summary>
        /// Chạm thẻ: chỉ đổi chế độ qua API sẵn có (bật, đổi sang thẻ khác, hoặc bấm lại để về Move).
        /// Không tiêu hao, không tăng lượt, không chạy pha địch; TurnInputController tự chặn khi chưa được phép.
        /// </summary>
        public static InputResponse Press(TurnInputController input, CardKind kind)
        {
            switch (kind)
            {
                case CardKind.HeroDash: return input.PressDash();
                case CardKind.SupportStasis: return input.PressStasis();
                default: return input.PressBarrier();
            }
        }
    }
}
