namespace StardewGallery;

internal interface IGallerySearchMenu
{
    bool IsSearchSelected { get; }
    void DeselectSearch();
    void OpenFirstMatch();
    void HandleControllerBack();
}
