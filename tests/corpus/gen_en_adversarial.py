#!/usr/bin/env python3
"""Generate adversarial EN corpus for Alak (QLT-001, M10-prep GOAL E).

10 adversarial categories (min 6 cases each = 60+ total):
1. obfuscation - spaced letters, zero-width joiners, homoglyphs
2. mixed_scripts - Latin + Arabic + Cyrillic in one notification
3. punctuation_glue - no spaces: '50%offcode:ABC123'
4. rtl_marks - U+200F, U+202E, U+202D embedded
5. emoji_only - 🔥💰🎁 — no letters
6. empty_title - body only
7. large_body - ~5 KB text (stress long body)
8. truncated_text - mid-word cutoff '…'
9. inbox_style_lines - multi-line summary (text_lines array)
10. messaging_style_threads - conversation bubbles

Output: tests/corpus/en/adversarial.json (standalone; combined by alak-pack coverage)
Schema per case: {id, package, channel_name, title, text, expect: {category, adversarial_type}}
"""
import json
import os
import random
import string

random.seed(20261003)

OUT = os.path.join(os.path.dirname(__file__), "en", "adversarial.json")

# Zero-width characters
ZWJ = "\u200D"      # Zero Width Joiner
ZWNJ = "\u200C"     # Zero Width Non-Joiner
RLM = "\u200F"      # Right-to-Left Mark
LRM = "\u200E"      # Left-to-Right Mark
RLO = "\u202E"      # Right-to-Left Override
LRO = "\u202D"      # Left-to-Right Override
PDF = "\u202C"      # Pop Directional Formatting

# Homoglyphs (Cyrillic/Greek lookalikes)
HOMO = {
    'A': '\u0410',  # Cyrillic A
    'B': '\u0412',  # Cyrillic Ve
    'C': '\u0421',  # Cyrillic Es
    'E': '\u0415',  # Cyrillic Ie
    'H': '\u041D',  # Cyrillic En
    'K': '\u041A',  # Cyrillic Ka
    'M': '\u041C',  # Cyrillic Em
    'O': '\u041E',  # Cyrillic O
    'P': '\u0420',  # Cyrillic Er
    'T': '\u0422',  # Cyrillic Te
    'X': '\u0425',  # Cyrillic Ha
    'a': '\u0430',  # Cyrillic a
    'c': '\u0441',  # Cyrillic es
    'e': '\u0435',  # Cyrillic ie
    'o': '\u043E',  # Cyrillic o
    'p': '\u0440',  # Cyrillic er
    'x': '\u0445',  # Cyrillic ha
    'y': '\u0443',  # Cyrillic u
}

def homoglyph_replace(text: str, ratio: float = 0.3) -> str:
    """Replace some letters with homoglyphs."""
    result = []
    for ch in text:
        if ch in HOMO and random.random() < ratio:
            result.append(HOMO[ch])
        else:
            result.append(ch)
    return ''.join(result)

def spaced_letters(text: str) -> str:
    """Insert spaces between letters: 'SPRING' -> 'S P R I N G'."""
    return ' '.join(text)

def zwj_inject(text: str) -> str:
    """Inject zero-width joiners between characters."""
    return ZWJ.join(text)

def rtl_embed(text: str) -> str:
    """Embed RTL marks throughout text."""
    result = []
    for ch in text:
        result.append(ch)
        if random.random() < 0.15:
            result.append(RLM)
    return ''.join(result)

def rlo_wrap(text: str) -> str:
    """Wrap text with RLO/PDF for RTL override."""
    return RLO + text + PDF

# ─────────────────────────────────────────────────────────────────────────────
# Category 1: Obfuscation (spaced letters, zero-width joiners, homoglyphs)
# ─────────────────────────────────────────────────────────────────────────────
def gen_obfuscation(n: int = 6):
    cases = []
    base_cases = [
        ("test.shop", "Promotions", "Spring Sale", "25% off with code SPRING25"),
        ("test.shop", "Deals", "Flash Sale", "Use code SAVE20 for 20% discount"),
        ("test.bank", "Security", "Verification", "Your code is 482913"),
        ("test.app", "Updates", "New Version", "Update to v2.5.0 now"),
        ("test.ride", "Orders", "Ride Arriving", "Driver is 3 min away"),
        ("test.food", "Offers", "Free Delivery", "Code FREESHIP for free delivery"),
        ("test.store", "Sale", "Clearance", "70% off everything today"),
        ("test.game", "Rewards", "Bonus", "Claim 500 coins with code BONUS500"),
    ]
    for i in range(n):
        pkg, chan, title, text = base_cases[i % len(base_cases)]
        variant = i // len(base_cases)
        if variant == 0:
            # Spaced letters in code/keywords
            adv_text = text.replace("SPRING25", spaced_letters("SPRING25"))
            adv_text = adv_text.replace("SAVE20", spaced_letters("SAVE20"))
            adv_text = adv_text.replace("482913", spaced_letters("482913"))
            adv_text = adv_text.replace("FREESHIP", spaced_letters("FREESHIP"))
            adv_text = adv_text.replace("BONUS500", spaced_letters("BONUS500"))
            adv_title = spaced_letters(title.upper())
        elif variant == 1:
            # Zero-width joiners
            adv_text = zwj_inject(text)
            adv_title = zwj_inject(title)
        else:
            # Homoglyph substitution
            adv_text = homoglyph_replace(text, 0.4)
            adv_title = homoglyph_replace(title, 0.4)
        
        cases.append({
            "id": f"en-adv-obf-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": adv_title,
            "text": adv_text,
            "expect": {"category": "offers" if "off" in text.lower() or "sale" in text.lower() or "discount" in text.lower() or "code" in text.lower() else "security" if "code" in text.lower() or "verification" in text.lower() else "updates", "adversarial_type": "obfuscation"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 2: Mixed Scripts (Latin + Arabic + Cyrillic)
# ─────────────────────────────────────────────────────────────────────────────
def gen_mixed_scripts(n: int = 6):
    cases = []
    arabic_words = ["تخفیف", "کد", "خرید", "فروشگاه", "پیشنهاد", "اعتبار", "بانک", "کارت"]
    cyrillic_words = ["магазин", "скидка", "код", "покупка", "предложение", "банк", "карта"]
    
    base_cases = [
        ("test.shop", "Promotions", "Sale", "25% off with code"),
        ("test.bank", "Security", "Alert", "Your verification code is"),
        ("test.app", "Updates", "Update", "New version available"),
        ("test.ride", "Orders", "Ride", "Driver arriving in"),
        ("test.food", "Offers", "Delivery", "Free delivery with code"),
        ("test.store", "News", "News", "Check out our latest"),
    ]
    
    for i in range(n):
        pkg, chan, title, text = base_cases[i % len(base_cases)]
        ar_word = random.choice(arabic_words)
        cy_word = random.choice(cyrillic_words)
        
        # Mix scripts in title and text
        adv_title = f"{title} {ar_word} {cy_word}"
        adv_text = f"{text} {ar_word} {cy_word} ABC123 {random.randint(1000,9999)}"
        
        cases.append({
            "id": f"en-adv-mixed-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": adv_title,
            "text": adv_text,
            "expect": {"category": "offers" if "off" in text.lower() or "sale" in text.lower() or "code" in text.lower() or "delivery" in text.lower() else "security" if "verification" in text.lower() or "alert" in text.lower() else "updates", "adversarial_type": "mixed_scripts"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 3: Punctuation Glue (no spaces)
# ─────────────────────────────────────────────────────────────────────────────
def gen_punctuation_glue(n: int = 6):
    cases = []
    glue_cases = [
        ("test.shop", "Promotions", "Sale", "50%offcode:SPRING25"),
        ("test.shop", "Deals", "Flash", "20%offuse:SAVE20now"),
        ("test.bank", "Security", "Alert", "Code:482913verify"),
        ("test.app", "Updates", "New", "v2.5.0available:update"),
        ("test.ride", "Orders", "Ride", "Driver:3mineta:arriving"),
        ("test.food", "Offers", "Delivery", "Free:codeFREESHIPapply"),
        ("test.store", "Sale", "Clearance", "70%off:everything:today"),
        ("test.game", "Rewards", "Bonus", "500coins:codeBONUS500"),
    ]
    
    for i in range(n):
        pkg, chan, title, text = glue_cases[i % len(glue_cases)]
        # Add more variations
        variations = [
            text,
            text.replace(":", "").replace("%", "pct"),
            text.upper(),
            text.lower(),
            text + "!!!",
            "!!" + text + "??",
        ]
        
        cases.append({
            "id": f"en-adv-glue-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": title.replace(" ", ""),
            "text": variations[i % len(variations)],
            "expect": {"category": "offers" if "off" in text.lower() or "code" in text.lower() or "free" in text.lower() or "coin" in text.lower() else "security" if "verify" in text.lower() or "code" in text.lower() else "updates", "adversarial_type": "punctuation_glue"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 4: RTL Marks (U+200F, U+202E, U+202D embedded)
# ─────────────────────────────────────────────────────────────────────────────
def gen_rtl_marks(n: int = 6):
    cases = []
    base_cases = [
        ("test.shop", "Promotions", "Spring Sale", "25% off with code SPRING25"),
        ("test.bank", "Security", "Bank Alert", "Your code is 482913"),
        ("test.app", "Updates", "App Update", "Version 2.5.0 available"),
        ("test.ride", "Orders", "Ride Status", "Driver arriving in 3 min"),
        ("test.food", "Offers", "Free Delivery", "Use code FREESHIP"),
        ("test.store", "News", "Clearance", "70% off everything"),
    ]
    
    rtl_variants = [RLM, RLO, LRO, RLM + RLM, RLO + "text" + PDF]
    
    for i in range(n):
        pkg, chan, title, text = base_cases[i % len(base_cases)]
        rtl_char = rtl_variants[i % len(rtl_variants)]
        
        # Inject RTL marks at various positions
        adv_title = ""
        for j, ch in enumerate(title):
            adv_title += ch
            if j % 3 == 0:
                adv_title += rtl_char
        
        adv_text = ""
        for j, ch in enumerate(text):
            adv_text += ch
            if j % 4 == 0:
                adv_text += rtl_char
        
        cases.append({
            "id": f"en-adv-rtl-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": adv_title,
            "text": adv_text,
            "expect": {"category": "offers" if "off" in text.lower() or "code" in text.lower() or "sale" in text.lower() else "security" if "code" in text.lower() or "alert" in text.lower() else "updates", "adversarial_type": "rtl_marks"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 5: Emoji Only (no letters)
# ─────────────────────────────────────────────────────────────────────────────
def gen_emoji_only(n: int = 6):
    cases = []
    emoji_sets = [
        "🔥💰🎁🎉💯",
        "💸🛍️🏷️💰🔥",
        "🚀⚡💎🎁💯",
        "💰💸🤑💵💴",
        "🎉🎊🥳🎁🎈",
        "🔥⚡💥💯🚀",
        "💎💍💰💸🤑",
        "🛍️🏷️💰🔖💸",
        "⚡🚀💥🔥💯",
        "🎁💰💸🔥💎",
    ]
    
    packages = ["test.shop", "test.app", "test.game", "test.store", "test.food", "test.ride"]
    channels = ["Promotions", "Rewards", "Offers", "Deals", "Flash Sale", "Specials"]
    
    for i in range(n):
        pkg = packages[i % len(packages)]
        chan = channels[i % len(channels)]
        emojis = emoji_sets[i % len(emoji_sets)]
        
        cases.append({
            "id": f"en-adv-emoji-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": emojis[:10],
            "text": emojis * 3,
            "expect": {"category": "offers", "adversarial_type": "emoji_only"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 6: Empty Title (body only)
# ─────────────────────────────────────────────────────────────────────────────
def gen_empty_title(n: int = 6):
    cases = []
    base_cases = [
        ("test.shop", "Promotions", "25% off everything with code SPRING25"),
        ("test.bank", "Security", "Your verification code is 482913. Do not share."),
        ("test.app", "Updates", "Version 2.5.0 is now available with bug fixes."),
        ("test.ride", "Orders", "Your driver is arriving in 3 minutes."),
        ("test.food", "Offers", "Free delivery on orders over $25 with code FREESHIP"),
        ("test.store", "News", "Clearance sale: 70% off all items today only."),
        ("test.game", "Rewards", "Claim 500 bonus coins with code BONUS500"),
        ("test.bank", "Money", "$45.00 charged to card ending 4417 at STARBUCKS"),
    ]
    
    for i in range(n):
        pkg, chan, text = base_cases[i % len(base_cases)]
        
        cases.append({
            "id": f"en-adv-empty-title-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": "",  # Empty title
            "text": text,
            "expect": {"category": "offers" if "off" in text.lower() or "code" in text.lower() or "free" in text.lower() or "coin" in text.lower() or "sale" in text.lower() else "security" if "verification" in text.lower() or "code" in text.lower() else "money" if "charged" in text.lower() else "updates", "adversarial_type": "empty_title"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 7: Large Body (~5 KB text)
# ─────────────────────────────────────────────────────────────────────────────
def gen_large_body(n: int = 6):
    cases = []
    # Base text to repeat
    base_paragraphs = [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.",
        "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt.",
        "Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem.",
    ]
    
    packages = ["test.app", "test.news", "test.blog", "test.shop", "test.service", "test.platform"]
    channels = ["Updates", "Newsletter", "Blog", "Announcements", "Digest", "Notifications"]
    
    for i in range(n):
        pkg = packages[i % len(packages)]
        chan = channels[i % len(channels)]
        
        # Build ~5KB text by repeating paragraphs
        target_size = 5000
        text_parts = []
        while len(" ".join(text_parts)) < target_size:
            text_parts.append(random.choice(base_paragraphs))
        
        # Add a promotional code at the end to ensure it classifies as offers
        full_text = " ".join(text_parts)[:target_size] + " Code: BIGSALE50 for 50% off."
        
        cases.append({
            "id": f"en-adv-large-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": f"Long Article {i+1}",
            "text": full_text,
            "expect": {"category": "offers", "adversarial_type": "large_body"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 8: Truncated Text (mid-word cutoff with ellipsis)
# ─────────────────────────────────────────────────────────────────────────────
def gen_truncated_text(n: int = 6):
    cases = []
    base_cases = [
        ("test.shop", "Promotions", "Spring Sale", "25% off everything with code SPRING25 ends tonight"),
        ("test.bank", "Security", "Bank Alert", "Your verification code is 482913 do not share with anyone"),
        ("test.app", "Updates", "App Update", "Version 2.5.0 available with new features and bug fixes"),
        ("test.ride", "Orders", "Ride Status", "Your driver is arriving in 3 minutes please be ready"),
        ("test.food", "Offers", "Free Delivery", "Free delivery on orders over $25 with code FREESHIP"),
        ("test.store", "News", "Clearance", "70% off everything today only while supplies last"),
    ]
    
    for i in range(n):
        pkg, chan, title, text = base_cases[i % len(base_cases)]
        
        # Truncate at various points mid-word
        words = text.split()
        truncate_at = random.randint(3, max(4, len(words) - 1))
        truncated = " ".join(words[:truncate_at])
        # Cut mid-word
        if len(truncated) > 10:
            cut_point = random.randint(5, min(len(truncated) - 3, len(truncated) - 1))
            truncated = truncated[:cut_point] + "…"
        
        cases.append({
            "id": f"en-adv-trunc-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": title,
            "text": truncated,
            "expect": {"category": "offers" if "off" in text.lower() or "code" in text.lower() or "free" in text.lower() or "sale" in text.lower() else "security" if "verification" in text.lower() or "alert" in text.lower() else "updates", "adversarial_type": "truncated_text"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 9: InboxStyle Lines (multi-line summary)
# ─────────────────────────────────────────────────────────────────────────────
def gen_inbox_style_lines(n: int = 6):
    cases = []
    # InboxStyle uses multiple lines (like email preview with multiple lines)
    inbox_cases = [
        ("test.mail", "Email", "New Messages", [
            "From: john@example.com — Meeting at 2pm",
            "From: sarah@example.com — Please review the doc",
            "From: team@example.com — Sprint retrospective tomorrow",
        ]),
        ("test.chat", "Messages", "Group Chat", [
            "Alice: Hey everyone!",
            "Bob: What's the plan for today?",
            "Charlie: Meeting in 10 mins",
        ]),
        ("test.app", "Notifications", "Activity", [
            "You have 3 new followers",
            "Your post got 50 likes",
            "New comment on your photo",
        ]),
        ("test.shop", "Orders", "Order Updates", [
            "Order #12345 shipped",
            "Order #12346 delivered",
            "Order #12347 processing",
        ]),
        ("test.bank", "Alerts", "Transactions", [
            "$25.00 at STARBUCKS",
            "$45.00 at AMAZON",
            "$12.50 at UBER",
        ]),
        ("test.social", "Social", "Activity Feed", [
            "John liked your photo",
            "Sarah commented on your post",
            "Mike shared your story",
        ]),
    ]
    
    for i in range(n):
        pkg, chan, title, lines = inbox_cases[i % len(inbox_cases)]
        # Join with newlines to simulate InboxStyle
        full_text = "\n".join(lines)
        
        cases.append({
            "id": f"en-adv-inbox-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": title,
            "text": full_text,
            "expect": {"category": "work" if "meeting" in title.lower() or "meeting" in full_text.lower() else "social" if "comment" in full_text.lower() or "like" in full_text.lower() or "follow" in full_text.lower() else "orders" if "order" in full_text.lower() or "shipped" in full_text.lower() else "money" if "$" in full_text else "updates", "adversarial_type": "inbox_style_lines"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Category 10: MessagingStyle Threads (conversation bubbles)
# ─────────────────────────────────────────────────────────────────────────────
def gen_messaging_style_threads(n: int = 6):
    cases = []
    # MessagingStyle shows conversation bubbles with sender names
    thread_cases = [
        ("test.chat", "Messages", "Team Chat", [
            "Sarah: The deadline is Friday",
            "Mike: Got it, I'll finish by Thursday",
            "Sarah: Perfect, thanks!",
        ]),
        ("test.ride", "Messages", "Driver Chat", [
            "Driver: I'm at the pickup location",
            "You: Great, coming down now",
            "Driver: See you in 2 min",
        ]),
        ("test.support", "Support", "Customer Support", [
            "Agent: How can I help you?",
            "User: My order #12345 hasn't arrived",
            "Agent: Let me check that for you",
        ]),
        ("test.family", "Family", "Family Group", [
            "Mom: Dinner at 7?",
            "Dad: Sounds good",
            "You: I'll be there",
        ]),
        ("test.work", "Work", "Project Channel", [
            "PM: Sprint review at 3pm",
            "Dev: Demo ready",
            "PM: Great, see you there",
        ]),
        ("test.market", "Market", "Seller Chat", [
            "Seller: Item still available?",
            "Buyer: Yes, I'll take it",
            "Seller: Great, when to meet?",
        ]),
    ]
    
    for i in range(n):
        pkg, chan, title, messages = thread_cases[i % len(thread_cases)]
        # Format as conversation thread
        full_text = "\n".join(messages)
        
        cases.append({
            "id": f"en-adv-msg-{i+1:03d}",
            "package": pkg,
            "channel_name": chan,
            "title": title,
            "text": full_text,
            "expect": {"category": "work" if "deadline" in full_text.lower() or "sprint" in full_text.lower() or "project" in full_text.lower() else "orders" if "driver" in full_text.lower() or "pickup" in full_text.lower() else "people" if "support" in pkg or "family" in pkg or "mom" in full_text.lower() or "dad" in full_text.lower() else "social", "adversarial_type": "messaging_style_threads"}
        })
    return cases

# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────
def main():
    all_cases = []
    
    print("Generating adversarial cases...")
    all_cases.extend(gen_obfuscation(6))
    print(f"  obfuscation: 6 cases")
    
    all_cases.extend(gen_mixed_scripts(6))
    print(f"  mixed_scripts: 6 cases")
    
    all_cases.extend(gen_punctuation_glue(6))
    print(f"  punctuation_glue: 6 cases")
    
    all_cases.extend(gen_rtl_marks(6))
    print(f"  rtl_marks: 6 cases")
    
    all_cases.extend(gen_emoji_only(6))
    print(f"  emoji_only: 6 cases")
    
    all_cases.extend(gen_empty_title(6))
    print(f"  empty_title: 6 cases")
    
    all_cases.extend(gen_large_body(6))
    print(f"  large_body: 6 cases")
    
    all_cases.extend(gen_truncated_text(6))
    print(f"  truncated_text: 6 cases")
    
    all_cases.extend(gen_inbox_style_lines(6))
    print(f"  inbox_style_lines: 6 cases")
    
    all_cases.extend(gen_messaging_style_threads(6))
    print(f"  messaging_style_threads: 6 cases")
    
    # Verify all 10 adversarial types present
    types_found = set(c["expect"]["adversarial_type"] for c in all_cases)
    expected_types = {
        "obfuscation", "mixed_scripts", "punctuation_glue", "rtl_marks",
        "emoji_only", "empty_title", "large_body", "truncated_text",
        "inbox_style_lines", "messaging_style_threads"
    }
    
    print(f"\nTotal cases: {len(all_cases)}")
    print(f"Types found: {len(types_found)}/10")
    print(f"Missing: {expected_types - types_found}")
    
    # Write output
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(all_cases, f, ensure_ascii=False, indent=2)
    
    print(f"\nWritten to {OUT}")
    
    # Output summary
    result = {
        "passed": len(all_cases) >= 60 and len(types_found) == 10,
        "adversarial_cases_generated": len(all_cases),
        "categories_covered": len(types_found),
        "file_path": OUT
    }
    print(json.dumps(result, indent=2))
    return result

if __name__ == "__main__":
    main()