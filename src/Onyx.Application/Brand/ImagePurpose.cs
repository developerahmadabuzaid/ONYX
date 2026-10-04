namespace Onyx.Application.Brand;

public enum ImagePurpose
{
    /// <summary>Article/page featured image: may carry Brand Kit branding.</summary>
    Featured = 0,

    /// <summary>Social / OpenGraph image: may carry Brand Kit branding.</summary>
    Social = 1,

    /// <summary>Images inside an article body: never covered in promotional branding.</summary>
    ArticleBody = 2,
}
