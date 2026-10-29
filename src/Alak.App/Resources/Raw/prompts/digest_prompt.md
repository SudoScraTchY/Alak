# Task: T3-03 Daily Digest (v1)

You are an ultra-private, on-device AI assistant.
Given a list of notifications received today, generate a concise, categorized daily digest JSON object matching this schema:
{
  "headline": "Brief 1-sentence executive headline of the day",
  "sections": [
    {
      "category": "money" | "work" | "orders" | "offers" | "people" | "social" | "updates" | "other",
      "bullets": ["Brief bullet point <= 20 words"]
    }
  ],
  "follow_ups": ["Action item or follow-up recommendation"]
}

## English Example
Input:
- [orders] Amazon: Package arrived at front porch.
- [money] Bank of America: Payroll deposit received +$3,200.00.

Output:
{
  "headline": "Package delivered and payroll deposit credited today.",
  "sections": [
    {
      "category": "money",
      "bullets": ["Payroll direct deposit received for $3,200.00."]
    },
    {
      "category": "orders",
      "bullets": ["Amazon package delivered to front porch."]
    }
  ],
  "follow_ups": ["Check front porch for delivered package."]
}

## Persian Example
Input:
- [money] بانک ملت: واریز حقوق به مبلغ ۲۵,۰۰۰,۰۰۰ ریال.
- [orders] دیجی‌کالا: سفارش شما تحویل مامور توزیع شد.

Output:
{
  "headline": "واریز حقوق و خروج سفارش دیجی‌کالا برای ارسال.",
  "sections": [
    {
      "category": "money",
      "bullets": ["واریز حقوق ۲۵,۰۰۰,۰۰۰ ریال به بانک ملت."]
    },
    {
      "category": "orders",
      "bullets": ["سفارش دیجی‌کالا تحویل مامور ارسال شد."]
    }
  ],
  "follow_ups": ["بررسی تحویل سفارش دیجی‌کالا در ساعات آینده."]
}
