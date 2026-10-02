using System;
using System.Collections.Generic;

namespace HeroEscape.Unity
{
    /// <summary>Vị trí đích của một thẻ trong tay, theo đơn vị canvas, so với điểm giữa đáy vùng tay bài (pivot thẻ ở giữa cạnh dưới).</summary>
    public readonly struct CardPose : IEquatable<CardPose>
    {
        public readonly float X;
        public readonly float Y;

        /// <summary>Góc xoay quanh trục Z (độ). Dương = ngược chiều kim đồng hồ, nên thẻ bên phải có góc âm.</summary>
        public readonly float Angle;

        public readonly float Scale;

        public CardPose(float x, float y, float angle, float scale)
        {
            X = x;
            Y = y;
            Angle = angle;
            Scale = scale;
        }

        public bool Equals(CardPose other) => X == other.X && Y == other.Y && Angle == other.Angle && Scale == other.Scale;
        public override bool Equals(object obj) => obj is CardPose other && Equals(other);
        public override int GetHashCode() => (X, Y, Angle, Scale).GetHashCode();
        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Angle:0.#}°, x{Scale:0.##})";
    }

    /// <summary>Thông số bố cục tay bài (đơn vị canvas tham chiếu 1080 x 1920). Chỉnh trong Inspector của HandView.</summary>
    public struct HandLayoutSettings
    {
        /// <summary>Khoảng cách tâm giữa hai vị trí kề nhau.</summary>
        public float SlotSpacing;

        /// <summary>Khoảng cách thêm giữa thẻ Hero và thẻ kề nó (Hero to hơn).</summary>
        public float HeroExtraSpacing;

        /// <summary>Độ nghiêng mỗi vị trí ra xa tâm.</summary>
        public float TiltPerSlot;

        /// <summary>Độ hạ thấp theo bình phương khoảng cách vị trí, tạo đường cong quạt.</summary>
        public float DropPerSlot;

        /// <summary>Vị trí cạnh dưới của thẻ ở trạng thái nghỉ; âm = thụt xuống dưới mép vùng an toàn.</summary>
        public float RestY;

        public float HeroScale;

        /// <summary>Độ nâng khi được chọn, đo dọc theo trục đứng của chính thẻ (thẻ nghiêng giữ nguyên góc).</summary>
        public float LiftHeight;

        public float SelectedScale;

        public static HandLayoutSettings Default => new HandLayoutSettings
        {
            SlotSpacing = 230f,
            HeroExtraSpacing = 18f,
            TiltPerSlot = 8f,
            DropPerSlot = 16f,
            RestY = -130f,
            HeroScale = 1.15f,
            LiftHeight = 145f,
            SelectedScale = 1f,
        };
    }

    /// <summary>
    /// Tính bố cục quạt của tay bài. Hàm thuần: cùng đầu vào luôn cho cùng vị trí, nên animation luôn tween tới
    /// vị trí chuẩn và không bao giờ cộng dồn offset.
    /// - Có Hero: Hero ở chính giữa, thẳng; phụ trợ lần lượt bên phải, bên trái, rồi xa dần. Không dịch Hero để căn giữa cả nhóm.
    /// - Không có Hero: các phụ trợ căn giữa; chỉ một thẻ thì ở giữa và thẳng.
    /// - Thẻ được chọn nâng lên theo trục của nó; Hero nâng thẳng trên trục giữa.
    /// </summary>
    public static class HandLayout
    {
        /// <param name="cards">Thẻ theo thứ tự <see cref="CardHand.From"/> (Hero trước).</param>
        public static CardPose[] Compute(IReadOnlyList<CardState> cards, HandLayoutSettings settings)
        {
            var poses = new CardPose[cards.Count];
            float[] slots = Slots(cards);
            bool heroPresent = HasHero(cards);

            for (int i = 0; i < cards.Count; i++)
            {
                float slot = slots[i];
                float x = slot * settings.SlotSpacing;
                if (heroPresent && slot != 0f) x += Math.Sign(slot) * settings.HeroExtraSpacing;
                float y = settings.RestY - slot * slot * settings.DropPerSlot;
                float angle = slot == 0f ? 0f : -slot * settings.TiltPerSlot;
                float scale = cards[i].IsHero ? settings.HeroScale : 1f;

                if (cards[i].Selected)
                {
                    // Trục đứng của thẻ sau khi xoay angle độ quanh Z: (-sin, cos).
                    double radians = angle * Math.PI / 180.0;
                    x += (float)(-Math.Sin(radians) * settings.LiftHeight);
                    y += (float)(Math.Cos(radians) * settings.LiftHeight);
                    scale *= settings.SelectedScale;
                }

                poses[i] = new CardPose(x, y, angle, scale);
            }

            return poses;
        }

        /// <summary>
        /// Thứ tự vẽ (chỉ số trong <paramref name="cards"/>, vẽ trước → sau). Thẻ xa tâm vẽ trước, Hero vẽ trên các phụ trợ,
        /// thẻ đang chọn vẽ sau cùng. Raycast của uGUI theo đúng thứ tự này nên vùng chồng nhau nhận input của thẻ nằm trên.
        /// </summary>
        public static int[] DrawOrder(IReadOnlyList<CardState> cards)
        {
            float[] slots = Slots(cards);
            var order = new List<int>(cards.Count);
            for (int i = 0; i < cards.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int bySelection = cards[a].Selected.CompareTo(cards[b].Selected);
                if (bySelection != 0) return bySelection;
                int byHero = cards[a].IsHero.CompareTo(cards[b].IsHero);
                if (byHero != 0) return byHero;
                int byDistance = Math.Abs(slots[b]).CompareTo(Math.Abs(slots[a]));
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
            return order.ToArray();
        }

        /// <summary>Vị trí (đơn vị slot, 0 = giữa) của từng thẻ.</summary>
        static float[] Slots(IReadOnlyList<CardState> cards)
        {
            var slots = new float[cards.Count];
            if (HasHero(cards))
            {
                int support = 0;
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i].IsHero) continue;
                    // Phụ trợ thứ 0 → +1 (phải), 1 → -1 (trái), 2 → +2, 3 → -2, ...
                    int distance = support / 2 + 1;
                    slots[i] = support % 2 == 0 ? distance : -distance;
                    support++;
                }
            }
            else
            {
                float center = (cards.Count - 1) * 0.5f;
                for (int i = 0; i < cards.Count; i++) slots[i] = i - center;
            }

            return slots;
        }

        static bool HasHero(IReadOnlyList<CardState> cards)
        {
            foreach (CardState card in cards)
                if (card.IsHero) return true;
            return false;
        }
    }
}
