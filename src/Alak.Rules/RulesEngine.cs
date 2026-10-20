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

    /// <summary>Built-in Persian pack loaded from compiled packs/fa/lexicon.json.</summary>
    public static LanguagePack BuiltInFaPack { get; } = LoadBuiltInFaPack();

    /// <summary>Persian pack set.</summary>
    public static PackSet BuiltInFa { get; } = Compile(new[] { BuiltInFaPack });

    /// <summary>Combined default pack set (EN + FA) used by RulesEngine.</summary>
    public static PackSet BuiltInDefault { get; } = Compile(new[] { BuiltInEn.Packs[0], BuiltInFaPack });

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

    private static LanguagePack LoadBuiltInFaPack()
    {
        var asm = typeof(PackSet).Assembly;
        using var stream = asm.GetManifestResourceStream("packs.fa.lexicon.json")
                        ?? asm.GetManifestResourceStream("Alak.Rules.packs.fa.lexicon.json");
        if (stream is not null)
        {
            return LoadPackFromStream(stream, "fa");
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "packs", "fa", "lexicon.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "packs", "fa", "lexicon.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "packs", "fa", "lexicon.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "packs", "fa", "lexicon.json"),
        };
        foreach (var p in candidates)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    using var fs = File.OpenRead(full);
                    return LoadPackFromStream(fs, "fa");
                }
            }
            catch { }
        }
        throw new InvalidOperationException("Could not load packs/fa/lexicon.json from embedded resource or disk.");
    }

    internal static LanguagePack LoadPackFromStream(Stream stream, string id)
    {
        using var doc = JsonDocument.Parse(stream);
        var dict = new Dictionary<string, IReadOnlyList<(string, float)>>();
        foreach (var concept in doc.RootElement.GetProperty("concepts").EnumerateObject())
        {
            var weight = concept.Value.TryGetProperty("weight", out var w) ? (float)w.GetDouble() : 0.9f;
            var terms = new List<(string, float)>();
            foreach (var t in concept.Value.GetProperty("terms").EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String)
                {
                    terms.Add((t.GetString()!, weight));
                }
                else
                {
                    terms.Add((t.GetProperty("t").GetString()!,
                        t.TryGetProperty("w", out var tw) ? (float)tw.GetDouble() : weight));
                }
            }
            dict[concept.Name] = terms;
        }
        return new LanguagePack(id, dict);
    }
}

/// <summary>
/// Tier 1: deterministic rules engine (spec §8.7–8.8). Normalize → match → extract →
/// score in log-odds → softmax decision → hard overrides (RUL-018).
/// </summary>
public static class RulesEngine
{
    private static readonly Category[] s_categories = Enum.GetValues<Category>();

    /// <summary>Per-category counts from this app's ingestion history (RUL-011).
    /// Null = no history yet (fresh app) ⇒ prior contributes nothing.</summary>
    public sealed record AppPrior(int[] Counts, int Total);

    public static ClassificationResult Classify(RawNotification n) =>
        Classify(n, PackSet.BuiltInDefault, null);

    public static ClassificationResult Classify(RawNotification n, AppPrior? prior) =>
        Classify(n, PackSet.BuiltInDefault, prior);

    public static ClassificationResult Classify(RawNotification n, PackSet packs) =>
        Classify(n, packs, null);

    public static ClassificationResult Classify(RawNotification n, PackSet packs, AppPrior? prior)
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

        var hasPersian = title.Folded.Any(c => c is >= '\u0600' and <= '\u06FF') ||
                         text.Folded.Any(c => c is >= '\u0600' and <= '\u06FF') ||
                         bigText.Folded.Any(c => c is >= '\u0600' and <= '\u06FF');
        var effectivePacks = packs;
        if (hasPersian && !packs.Packs.Any(p => p.Id == "fa"))
        {
            effectivePacks = PackSet.BuiltInDefault;
        }

        // ---- 2. concept matching (RUL-003) ----
        var titleHits = effectivePacks.Matcher.Scan(title.Folded).ToList();
        var textHits = effectivePacks.Matcher.Scan(text.Folded).ToList();
        var bigTextHits = effectivePacks.Matcher.Scan(bigText.Folded).ToList();

        var hits = new List<ConceptHit>(titleHits.Count + textHits.Count + bigTextHits.Count);
        hits.AddRange(titleHits);
        hits.AddRange(textHits);
        hits.AddRange(bigTextHits);

        var channelText = Normalizer.Normalize((n.ChannelName ?? n.ChannelId ?? ""));
        var channelHits = effectivePacks.Matcher.Scan(channelText.Folded).ToList();

        // ---- 3. extraction (§8.7) ----
        var otp = OtpExtractor.Extract(text, textHits) ?? (!string.IsNullOrWhiteSpace(title.Text) ? OtpExtractor.Extract(title, titleHits) : null);
        var entities = new List<Entity>();
        if (otp is not null) entities.Add(otp);

        entities.AddRange(DiscountExtractor.Extract(text, "text"));
        if (!string.IsNullOrWhiteSpace(title.Text))
            entities.AddRange(DiscountExtractor.Extract(title, "title"));

        entities.AddRange(OrderRefExtractor.Extract(text));
        if (otp is null)
        {
            var rawCodes = PromoCodeExtractor.Extract(text, textHits, otpPresent: false, globalHits: hits, field: "text");
            if (!string.IsNullOrWhiteSpace(title.Text))
            {
                rawCodes = rawCodes.Concat(PromoCodeExtractor.Extract(title, titleHits, otpPresent: false, globalHits: hits, field: "title"));
            }
            var codes = rawCodes
                .GroupBy(c => c.Code, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(c => c.Confidence).First());
            entities.AddRange(codes);
        }

        entities.AddRange(MoneyExtractor.Extract(text, textHits, "text"));
        if (!string.IsNullOrWhiteSpace(title.Text))
            entities.AddRange(MoneyExtractor.Extract(title, titleHits, "title"));

        entities.AddRange(DatetimeExtractor.Extract(text, n.PostedAtUtcMs, n.TzOffsetMin, "text"));
        if (!string.IsNullOrWhiteSpace(title.Text))
            entities.AddRange(DatetimeExtractor.Extract(title, n.PostedAtUtcMs, n.TzOffsetMin, "title"));

        var minSpend = MinSpendExtractor.Extract(text) ?? (!string.IsNullOrWhiteSpace(title.Text) ? MinSpendExtractor.Extract(title) : null);
        var storeHint = StoreExtractor.DetectStore(n, title, text);

        // E-01 step 6: attach nearest discount, minSpend, storeHint, validUntil to each promo code
        var discounts = entities.OfType<DiscountEntity>().ToList();
        var expiries = entities.OfType<DatetimeEntity>().Where(d => d.Role == DatetimeRole.Expiry).ToList();

        entities = entities.Select((Entity e) =>
        {
            if (e is PromoCodeEntity pc)
            {
                var updated = pc;
                if (updated.Discount is null && discounts.Count > 0)
                {
                    var sameField = discounts.Where(d => d.Field == pc.Field).ToList();
                    var cand = sameField.Count > 0 ? sameField : discounts;
                    var pcMid = (pc.Span.Start + pc.Span.End) / 2;
                    var best = cand.OrderBy(d => Math.Abs(((d.Span.Start + d.Span.End) / 2) - pcMid)).First();
                    updated = updated with { Discount = best };
                }
                if (updated.MinSpend is null && minSpend is not null)
                {
                    updated = updated with { MinSpend = minSpend };
                }
                if (updated.StoreHint is null && storeHint is not null)
                {
                    updated = updated with { StoreHint = storeHint };
                }
                if (updated.ValidUntil is null && expiries.Count > 0)
                {
                    var sameField = expiries.Where(d => d.Field == pc.Field).ToList();
                    var cand = sameField.Count > 0 ? sameField : expiries;
                    updated = updated with { ValidUntil = cand.First().Instant };
                }
                return updated;
            }
            return e;
        }).ToList();

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
                case "channel.work": Add(Category.Work, 1.8f); break;
                case "channel.calendar": Add(Category.Work, 1.6f); Add(Category.Updates, 0.3f); break;
                case "channel.orders": Add(Category.Orders, 1.5f); break;
                case "channel.money": Add(Category.Money, 1.5f); break;
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
                or "promo.claim" or "promo.event"
                    => (Category.Offers, 0.9f),
                "otp.trigger" or "otp.warning" or "security.login" or "security.password"
                    or "security.alert" or "security.2fa"
                    => (Category.Security, 0.9f),
                "orders.status" or "orders.tracking" or "orders.ride" or "orders.pin"
                    or "orders.delivery_person" or "orders.receipt" or "orders.flight"
                    or "orders.booking"
                    => (Category.Orders, 0.9f),
                "work.mention" => (Category.Work, 2.2f), // §8.10 #5: 'mentioned you in #channel' is strongly work
                "work.meeting" or "work.calendar" or "work.deadline"
                    or "work.review" or "work.build" or "work.incident" or "work.ticket"
                    or "work.doc" or "work.assign"
                    => (Category.Work, 0.9f),
                "money.debit" or "money.credit" or "money.transfer" or "money.balance"
                    or "money.bill" or "money.card" or "money.fraud" or "money.refund"
                    or "money.tax"
                    => (Category.Money, 0.9f),
                "social.reaction" or "social.follow" or "social.feed" or "social.live"
                    => (Category.Social, 0.9f),
                "updates.app" or "updates.system" or "updates.battery" or "updates.storage"
                    or "updates.weather" or "updates.news"
                    => (Category.Updates, 0.9f),
                "people.missed_call" or "people.voice_message" or "people.group"
                    or "people.greeting" or "people.direct_message" or "people.photo"
                    or "people.reaction"
                    => (Category.People, 0.9f),
                "promo.no_code" => (Category.Offers, 0.6f),
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
                case DiscountEntity: Add(Category.Offers, 1.2f); break;
                case OtpEntity: Add(Category.Security, 6.0f); break;
                case OrderRefEntity: Add(Category.Orders, 1.4f); break;
                case MoneyEntity: Add(Category.Money, 2.0f); break;
                case DatetimeEntity: Add(Category.Work, 0.6f); break;
            }
        }

        // emoji signals (NRM-009)
        if (text.EmojiSignals.Promo >= 1) Add(Category.Offers, 0.5f);

        // app prior (§8.8): λ·log((n_c+α)/(N+9α)), α=1, λ=0.6, clamped ±1.5 (RUL-011)
        if (prior is { Counts: var counts, Total: var total })
        {
            const float alpha = 1f, lambda = 0.6f;
            for (var i = 0; i < 9; i++)
            {
                var c = (Category)i;
                if (c == Category.Other) continue;
                var w = lambda * MathF.Log((counts[i] + alpha) / (total + 9 * alpha));
                var clamped = Math.Clamp(w, -1.5f, 1.5f);
                Add(c, clamped);
            }
        }

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
        // ride PIN: spec §8.10 #7 — share-PIN-for-driver is NOT an OTP.
        // A 4..8 digit token sits close to an orders.ride/pin hit (within 12 chars).
        var ridePin = hits.Any(h => h.ConceptId is "orders.ride" or "orders.pin")
            && Alak.Rules.Extraction.Tokens.Tokenize(text).Any(t =>
                   t.Value.Trim('.', ',', '!', ':').All(char.IsDigit)
                && t.Value.Trim('.', ',', '!', ':').Length is >= 4 and <= 8
                && hits.Any(h => (h.ConceptId is "orders.ride" or "orders.pin")
                    && Math.Abs(t.FoldedSpan.Start - h.Span.Start) <= 12));
        if (otp is not null && !ridePin)
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
        if (hits.Any(h => h.ConceptId == "promo.no_code")) tags.Add(new Tag("no-code-needed"));
        if (hits.Any(h => h.ConceptId == "orders.ride")) tags.Add(new Tag("ride"));
        if (entities.OfType<PromoCodeEntity>().Any(p => p.Kind == PromoCodeKind.Referral)) tags.Add(new Tag("referral"));
        if (hits.Any(h => h.ConceptId == "work.meeting")) tags.Add(new Tag("meeting"));
        if (hits.Any(h => h.ConceptId == "work.mention")) tags.Add(new Tag("mention"));
        // ride PIN: digit token near orders.ride/pin context → ride-pin (§8.10 #7)
        if (ridePin)
        {
            tags.Add(new Tag("ride"));
            tags.Add(new Tag("ride-pin"));
        }

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
