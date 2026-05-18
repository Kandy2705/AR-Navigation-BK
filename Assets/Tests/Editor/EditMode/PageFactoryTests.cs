#if UNITY_EDITOR
using NUnit.Framework;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Bonus – xác minh PageFactory định tuyến đúng controller cho các PageID cốt lõi.
    /// </summary>
    [Category("TestAR")]
    public sealed class PageFactoryTests
    {
        [Test]
        public void PageFactory_MainSettings_MapsToMainSettingController()
        {
            var ctrl = PageFactory.GetController(PageID.MainSettings);
            Assert.IsInstanceOf<MainSettingController>(ctrl,
                "PageID.MainSettings phải trả về MainSettingController.");
        }

        [Test]
        public void PageFactory_UnknownPage_ReturnsDefaultPageController()
        {
            // PageID.None không có entry trong switch → phải trả về DefaultPageController
            var ctrl = PageFactory.GetController(PageID.None);
            Assert.IsInstanceOf<DefaultPageController>(ctrl,
                "PageID không được khai báo phải dùng DefaultPageController.");
        }
    }
}
#endif
