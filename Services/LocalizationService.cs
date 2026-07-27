using System;
using System.Collections.Generic;

namespace MilkWala.Services
{
    public class LocalizationService
    {
        public string CurrentLanguage { get; private set; } = "en"; // "en", "mr", "hi"

        public event EventHandler? LanguageChanged;

        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["en"] = new()
            {
                ["AppName"] = "MilkWala",
                ["TodayDelivery"] = "Today's Delivery",
                ["WalletBalance"] = "Wallet Balance",
                ["PendingPayment"] = "Pending Payment",
                ["Subscriptions"] = "Subscriptions",
                ["Calendar"] = "Delivery Calendar",
                ["Orders"] = "Orders & Extra Milk",
                ["Complaints"] = "Complaints & Support",
                ["Delivered"] = "Delivered",
                ["Skipped"] = "Skipped",
                ["Holiday"] = "Holiday",
                ["Extra"] = "Extra Delivery",
                ["CowMilk"] = "Cow Milk",
                ["BuffaloMilk"] = "Buffalo Milk",
                ["TonedMilk"] = "Toned Milk",
                ["FullCream"] = "Full Cream",
                ["Organic"] = "Organic Milk",
                ["Recharge"] = "Recharge Wallet",
                ["RaiseComplaint"] = "Raise Complaint",
                ["TotalCustomers"] = "Total Customers",
                ["TotalCollection"] = "Total Collection",
                ["MonthlyIncome"] = "Monthly Revenue",
                ["Outstanding"] = "Outstanding Dues",
                ["InventoryStock"] = "Milk Inventory",
                ["Expenses"] = "Expenses",
                ["Reports"] = "Reports"
            },
            ["mr"] = new()
            {
                ["AppName"] = "मिल्कवाला",
                ["TodayDelivery"] = "आजचे दूध वितरण",
                ["WalletBalance"] = "वॉलेट शिल्लक",
                ["PendingPayment"] = "थकबाकी रक्कम्",
                ["Subscriptions"] = "सदस्यता व्यवस्थापन",
                ["Calendar"] = "वितरण कॅलेंडर",
                ["Orders"] = "ऑर्डर आणि अतिरिक्त दूध",
                ["Complaints"] = "तक्रार आणि मदत",
                ["Delivered"] = "वितरित झाले",
                ["Skipped"] = "रद्द केले",
                ["Holiday"] = "सुट्टी",
                ["Extra"] = "अतिरिक्त वितरण",
                ["CowMilk"] = "गाईचे दूध",
                ["BuffaloMilk"] = "म्हशीचे दूध",
                ["TonedMilk"] = "टोन दूध",
                ["FullCream"] = "फुल क्रीम दूध",
                ["Organic"] = "ऑर्गेनिक दूध",
                ["Recharge"] = "वॉलेट रिचार्ज करा",
                ["RaiseComplaint"] = "तक्रार नोंदवा",
                ["TotalCustomers"] = "एकूण ग्राहक",
                ["TotalCollection"] = "एकूण वसुली",
                ["MonthlyIncome"] = "मासिक उत्पन्न",
                ["Outstanding"] = "थकबाकी",
                ["InventoryStock"] = "दूध साठा",
                ["Expenses"] = "खर्च",
                ["Reports"] = "अहवाल"
            },
            ["hi"] = new()
            {
                ["AppName"] = "मिल्कवाला",
                ["TodayDelivery"] = "आज की डिलीवरी",
                ["WalletBalance"] = "वॉलेट बैलेंस",
                ["PendingPayment"] = "बकाया राशि",
                ["Subscriptions"] = "सदस्यता प्रबंधन",
                ["Calendar"] = "डिलीवरी कैलेंडर",
                ["Orders"] = "ऑर्डर और अतिरिक्त दूध",
                ["Complaints"] = "शिकायत और सहायता",
                ["Delivered"] = "डिलीवर हुआ",
                ["Skipped"] = "रद्द किया",
                ["Holiday"] = "छुट्टी",
                ["Extra"] = "अतिरिक्त डिलीवरी",
                ["CowMilk"] = "गाय का दूध",
                ["BuffaloMilk"] = "भैंस का दूध",
                ["TonedMilk"] = "टोंड दूध",
                ["FullCream"] = "फुल क्रीम दूध",
                ["Organic"] = "ऑर्गेनिक दूध",
                ["Recharge"] = "वॉलेट रिचार्ज करें",
                ["RaiseComplaint"] = "शिकायत दर्ज करें",
                ["TotalCustomers"] = "कुल ग्राहक",
                ["TotalCollection"] = "कुल वसूली",
                ["MonthlyIncome"] = "मासिक आय",
                ["Outstanding"] = "बकाया राशि",
                ["InventoryStock"] = "दूध स्टॉक",
                ["Expenses"] = "खर्च",
                ["Reports"] = "रिपोर्ट्स"
            }
        };

        public void SetLanguage(string langCode)
        {
            if (langCode == "en" || langCode == "mr" || langCode == "hi")
            {
                CurrentLanguage = langCode;
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Get(string key)
        {
            if (_translations.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }
            if (_translations["en"].TryGetValue(key, out var defaultVal))
            {
                return defaultVal;
            }
            return key;
        }
    }
}
