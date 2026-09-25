using System.Text.Json;
using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Rules.Extraction;
using Alak.Text;

namespace Alak.Rules;

/// <summary>A loaded language pack: terms grouped by concept (spec §8.5, data only).</summary>
public sealed record LanguagePack(
    string Id,
    IReadOnlyDictionary<string, IReadOnlyList<(string Term, float Weight)>> Concepts);

/// <summary>The immutable set of enabled packs + their compiled matcher (RUL-003).</summary>
public sealed class PackSet
{
    public required IReadOnlyList<LanguagePack> Packs { get; init; }
    public required ConceptMatcher Matcher { get; init; }

    /// <summary>English seed pack (§8.9.1 subset sufficient for worked examples).</summary>
    public static PackSet BuiltInEn { get; } = BuildEn();

    private static PackSet BuildEn()
    {
        var concepts = new Dictionary<string, IReadOnlyList<(string, float)>>
        {
            ["promo.code.trigger"] = new[] { ("promo code", 1f), ("coupon code", 1f), ("coupon", 0.9f),
                ("discount code", 1f), ("voucher", 0.9f), ("gift code", 1f), ("use code", 1f),
                ("with code", 0.95f), ("enter code", 1f), ("apply code", 1f), ("code", 0.6f) },
            // NOTE: bare 'code' handled as weak below; keep trigger strong list without it.
            ["promo.code.trigger.weak"] = new[] { ("code", 0.4f) },
            ["promo.code.negative"] = new[] { ("verification code", 1f), ("order code", 1f),
                ("tracking code", 1f), ("zip code", 1f), ("postal code", 1f), ("area code", 1f),
                ("country code", 1f), ("reference code", 1f), ("security code", 1f),
                ("confirmation code", 1f), ("order number", 1f) },
            ["promo.code.referral"] = new[] { ("referral code", 1f), ("invite code", 1f), ("refer a friend", 0.9f) },
            ["promo.code.gift"] = new[] { ("gift code", 1f) },
            ["promo.apply"] = new[] { ("at checkout", 0.9f), ("in your cart", 0.9f), ("redeem now", 0.9f),
                ("enter at checkout", 1f), ("apply at checkout", 1f), ("valid on", 0.8f) },
            ["promo.discount.word"] = new[] { ("sale", 0.8f), ("discount", 1f), ("off", 0.7f), ("deal", 0.8f),
                ("save", 0.7f), ("special offer", 1f), ("offer", 0.8f), ("clearance", 0.8f) },
            ["promo.free_shipping"] = new[] { ("free shipping", 1f), ("free delivery", 1f), ("ships free", 1f) },
            ["promo.bogo"] = new[] { ("bogo", 1f), ("buy one get one", 1f), ("2 for 1", 0.9f) },
            ["promo.cashback"] = new[] { ("cashback", 1f), ("cash back", 1f), ("money back", 0.9f) },
            ["promo.limited"] = new[] { ("limited time", 0.9f), ("today only", 1f), ("last chance", 1f),
                ("hurry", 0.8f), ("act now", 0.9f), ("ends soon", 0.9f), ("exclusive", 0.7f) },
            ["promo.urgency.time"] = new[] { ("ends tonight", 1f), ("ends today", 1f), ("last day", 0.9f),
                ("expires soon", 0.9f), ("only today", 0.9f) },
            ["promo.first_order"] = new[] { ("first order", 1f), ("first purchase", 1f), ("new customers", 0.9f) },
            ["otp.trigger"] = new[] { ("verification code", 1f), ("verify your", 0.9f), ("one-time code", 1f),
                ("login code", 1f), ("otp", 0.9f) },
            ["otp.warning"] = new[] { ("don't share it", 1f), ("do not share", 1f), ("never share", 1f) },
            ["otp.negative"] = new[] { ("in the app", 0.8f) },
            ["security.login"] = new[] { ("signed in", 0.9f), ("login", 0.9f), ("sign in", 0.9f) },
            ["security.password"] = new[] { ("password", 1f), ("reset your password", 1f) },
            ["orders.status"] = new[] { ("order", 0.7f), ("shipped", 0.9f), ("delivered", 0.9f),
                ("out for delivery", 1f), ("your order", 1f) },
            ["orders.tracking"] = new[] { ("tracking", 1f), ("track your package", 1f), ("track order", 1f) },
            ["orders.ride"] = new[] { ("your driver", 1f), ("ride", 0.7f), ("trip", 0.7f) },
            ["social.reaction"] = new[] { ("liked your", 0.9f), ("commented on", 0.9f), ("followed you", 0.9f) },
            ["updates.app"] = new[] { ("update available", 1f), ("new version", 1f), ("update now", 0.9f) },
            ["updates.system"] = new[] { ("battery low", 1f), ("storage almost full", 1f) },
            ["work.meeting"] = new[] { ("meeting", 0.9f), ("standup", 0.8f) },
            ["work.mention"] = new[] { ("mentioned you", 1f) },
            ["people.missed_call"] = new[] { ("missed call", 1f) },
            ["people.voice_message"] = new[] { ("voice message", 1f), ("voicemail", 1f) },
            ["channel.promo"] = new[] { ("promotions", 1f), ("offers", 1f), ("marketing", 1f), ("deals", 1f) },
            ["channel.messages"] = new[] { ("messages", 1f), ("sms", 0.8f) },
            ["channel.transactional"] = new[] { ("transactional", 1f), ("transactions", 1f) },
            ["channel.security"] = new[] { ("security", 1f) },
            ["channel.updates"] = new[] { ("updates", 1f) },
            ["channel.social"] = new[] { ("social", 1f) },
        };

        // remove bare 'code' from strong trigger list (it's the weak anchor)
        concepts["promo.code.trigger"] = concepts["promo.code.trigger"]
            .Where(t => t.Item1 != "code").ToArray();

        var pack = new LanguagePack("en", concepts);
        return Compile(new[] { pack });
    }

    public static PackSet Compile(IReadOnlyList<LanguagePack> packs)
    {
        var terms = new List<(string ConceptId, float Weight, string Term)>();
        foreach (var pack in packs)
            foreach (var (conceptId, terms_) in pack.Concepts)
                foreach (var (term, weight) in terms_)
                    terms.Add((conceptId, weight, term));
        return new PackSet { Packs = packs, Matcher = new ConceptMatcher(terms) };
    }
}

/// <summary>
/// Tier 1: deterministic rules engine (spec §8.7–8.8). Normalize → match → extract →
/// score in log-odds → softmax decision → hard overrides (RUL-018).
/// </summary>
public static class RulesEngine
{
    private static readonly Category[] s_categories = Enum.GetValues<Category>();

    public static ClassificationResult Classify(RawNotification n, PackSet packs)
    {
        // ---- 0. hard override first: OS-redacted content ⇒ security (RUL-018/1) ----
        if (n.LooksRedactedByOs)
            return new ClassificationResult(Category.Security, CategorySource.Rules, 1f, 1f, false,
                new[] { new Tag("security") }, Array.Empty<Evidence>(),
                new Entity[] { new OtpEntity(0, "text", default, 1f, "PF-02") });

        // ---- 1. normalize all fields (§8.2) ----
        var title = Normalizer.Normalize(n.Title ?? "");
        var text = Normalizer.Normalize(n.Text ?? "");
        var bigText = Normalizer.Normalize(n.BigText ?? "");

        // ---- 2. concept matching (RUL-003) ----
        var hits = new List<ConceptHit>();
        foreach (var h in packs.Matcher.Scan(title.Folded)) hits.Add(h);
        foreach (var h in packs.Matcher.Scan(text.Folded)) hits.Add(h);
        foreach (var h in packs.Matcher.Scan(bigText.Folded)) hits.Add(h);

        var channelText = Normalizer.Normalize((n.ChannelName ?? n.ChannelId ?? ""));
        var channelHits = packs.Matcher.Scan(channelText.Folded).ToList();

        // ---- 3. extraction (§8.7) ----
        var otp = OtpExtractor.Extract(text, hits);
        var entities = new List<Entity>();
        if (otp is not null) entities.Add(otp);
        entities.AddRange(DiscountExtractor.Extract(text));
        entities.AddRange(OrderRefExtractor.Extract(text));
        if (otp is null)
        {
            // dedupe by code value (multiple anchors may fire on the same candidate): keep best
            var codes = PromoCodeExtractor.Extract(text, hits, otpPresent: false)
                .GroupBy(c => c.Code, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(c => c.Confidence).First());
            entities.AddRange(codes);
        }

        // ---- 4. log-odds scoring (§8.8) ----
        var scores = new float[9];
        void Add(Category c, float w) => scores[(int)c] += w;

        // Android category metadata
        if (n.AndroidCategory is not null)
        {
            switch (n.AndroidCategory.ToLowerInvariant())
            {
                case "promo": Add(Category.Offers, 2.0f); break;
                case "recommendation": Add(Category.Offers, 1.0f); Add(Category.Social, 0.4f); break;
                case "message": Add(Category.People, 1.6f); break;
                case "email": Add(Category.Work, 0.7f); Add(Category.People, 0.4f); break;
                case "social": Add(Category.Social, 1.6f); break;
                case "event" or "reminder": Add(Category.Work, 0.6f); Add(Category.Updates, 0.3f); break;
                case "missed_call": Add(Category.People, 2.4f); break;
                case "status" or "system" or "error": Add(Category.Updates, 1.0f); break;
            }
        }

        // channel name/id → channel.* concepts
        foreach (var ch in channelHits)
        {
            switch (ch.ConceptId)
            {
                case "channel.promo": Add(Category.Offers, 1.8f); break;
                case "channel.messages": Add(Category.People, 1.8f); break;
                case "channel.transactional": Add(Category.Orders, 0.9f); Add(Category.Money, 0.9f); break;
                case "channel.security": Add(Category.Security, 1.8f); break;
                case "channel.updates": Add(Category.Updates, 1.8f); break;
                case "channel.social": Add(Category.Social, 1.8f); break;
            }
        }

        // lexicon family (capped ±3.0 per family)
        var lexByCategory = new float[9];
        foreach (var h in hits)
        {
            var (cat, weight) = h.ConceptId switch
            {
                "promo.code.trigger" or "promo.code.referral" or "promo.code.gift" or
                "promo.discount.word" or "promo.free_shipping" or "promo.bogo" or
                "promo.cashback" or "promo.limited" or "promo.urgency.time" or "promo.first_order"
                    => (Category.Offers, 0.9f),
                "otp.trigger" or "otp.warning" or "security.login" or "security.password"
                    => (Category.Security, 0.9f),
                "orders.status" or "orders.tracking" or "orders.ride"
                    => (Category.Orders, 0.9f),
                "work.meeting" or "work.mention"
                    => (Category.Work, 0.9f),
                "social.reaction" => (Category.Social, 0.9f),
                "updates.app" or "updates.system" => (Category.Updates, 0.9f),
                "people.missed_call" or "people.voice_message" => (Category.People, 0.9f),
                _ => (Category.Other, 0f),
            };
            if (cat != Category.Other)
                lexByCategory[(int)cat] += weight * h.Weight;
        }
        for (var i = 0; i < 9; i++)
            scores[i] += Math.Clamp(lexByCategory[i], -3f, 3f);

        // entity evidence
        foreach (var e in entities)
        {
            switch (e)
            {
                case PromoCodeEntity pc when pc.Confidence >= 0.6f: Add(Category.Offers, 2.4f); break;
                case DiscountEntity { Type: DiscountType.Percent or DiscountType.Amount }: Add(Category.Offers, 1.2f); break;
                case OtpEntity: Add(Category.Security, 6.0f); break;
                case OrderRefEntity: Add(Category.Orders, 1.4f); break;
            }
        }

        // emoji signals (NRM-009)
        if (text.EmojiSignals.Promo >= 1) Add(Category.Offers, 0.5f);

        // ---- 5. softmax decision (§8.8) ----
        var max = scores.Max();
        var exps = scores.Select(s => MathF.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        var p = exps.Select(e => e / sum).ToArray();
        var order = Enumerable.Range(0, 9)
            .Select(i => (Idx: i, P: p[i]))
            .OrderByDescending(x => x.P)
            .ThenBy(x => x.Idx) // priority order = enum order (security first)
            .ToArray();
        var top = order[0];
        var second = order[1];
        var category = (Category)top.Idx;
        var confidence = top.P;
        var margin = top.P - second.P;
        var needsReview = confidence < 0.55f || margin < 0.12f;
        if (confidence < 0.35f) category = Category.Other;

        // ---- 6. hard overrides (RUL-018) ----
        if (otp is not null)
        {
            category = Category.Security;
            needsReview = false;
            confidence = 1f;
        }

        // ---- tags from entities and concepts (§6.2) ----
        var tags = new List<Tag>();
        if (entities.OfType<PromoCodeEntity>().Any()) tags.Add(new Tag("promo-code"));
        if (entities.OfType<DiscountEntity>().Any(d => d.Type is DiscountType.Percent or DiscountType.Amount))
            tags.Add(new Tag("discount"));
        if (entities.OfType<DiscountEntity>().Any(d => d.Type == DiscountType.FreeShipping))
            tags.Add(new Tag("free-shipping"));
        if (entities.OfType<OtpEntity>().Any()) tags.Add(new Tag("verification-code"));
        if (entities.OfType<OrderRefEntity>().Any()) tags.Add(new Tag("delivery"));
        if (hits.Any(h => h.ConceptId == "promo.urgency.time")) tags.Add(new Tag("expiring"));

        // ---- evidence trace (RUL-020): top 6 by weight ----
        var evidence = new List<Evidence>();
        foreach (var h in hits.Take(20))
        {
            var (cat, w) = h.ConceptId switch
            {
                var c when c.StartsWith("promo.", StringComparison.Ordinal) => (Category.Offers, 0.9f * h.Weight),
                var c when c.StartsWith("otp.", StringComparison.Ordinal) || c.StartsWith("security.", StringComparison.Ordinal) => (Category.Security, 0.9f * h.Weight),
                var c when c.StartsWith("orders.", StringComparison.Ordinal) => (Category.Orders, 0.9f * h.Weight),
                var c when c.StartsWith("work.", StringComparison.Ordinal) => (Category.Work, 0.9f * h.Weight),
                var c when c.StartsWith("social.", StringComparison.Ordinal) => (Category.Social, 0.9f * h.Weight),
                var c when c.StartsWith("updates.", StringComparison.Ordinal) => (Category.Updates, 0.9f * h.Weight),
                _ => (Category.Other, 0f),
            };
            if (w != 0f)
                evidence.Add(new Evidence(cat, w, "RUL-003", "concept.hit",
                    new[] { h.ConceptId, h.Term }, h.Span));
        }
        foreach (var e in entities)
        {
            var w = e switch
            {
                PromoCodeEntity => 2.4f,
                OtpEntity => 6.0f,
                OrderRefEntity => 1.4f,
                DiscountEntity => 1.2f,
                _ => 0f,
            };
            if (w != 0f)
                evidence.Add(new Evidence(
                    e switch
                    {
                        PromoCodeEntity or DiscountEntity => Category.Offers,
                        OtpEntity => Category.Security,
                        OrderRefEntity => Category.Orders,
                        _ => Category.Other,
                    }, w, e.RuleId, "entity", new[] { e.GetType().Name }, e.Span));
        }

        return new ClassificationResult(category, CategorySource.Rules, confidence, margin, needsReview,
            tags, evidence, entities);
    }
}
