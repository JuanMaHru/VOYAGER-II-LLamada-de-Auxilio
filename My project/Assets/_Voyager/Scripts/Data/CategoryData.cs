using UnityEngine;

namespace Voyager
{
    /// <summary>Categoría de propuesta (Música, Comida, Animales, ...).</summary>
    [CreateAssetMenu(menuName = "Voyager/Category", fileName = "Category_")]
    public class CategoryData : ContentAsset
    {
        public Sprite icon;
        [TextArea] public string description;
    }
}
