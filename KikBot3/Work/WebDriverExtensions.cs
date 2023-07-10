using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace KikBot3.Work
{
    internal static class WebDriverExtensions
    {
        public static IWebElement FindElement(this IWebDriver driver, By by, int timeoutInSeconds)
        {
            try
            {
                if (timeoutInSeconds <= 0)
                    return driver.FindElement(@by);

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
                return wait.Until(drv => drv.FindElement(@by));
            }
            catch
            {
                //ignored
            }

            return null;
        }
    }
}
