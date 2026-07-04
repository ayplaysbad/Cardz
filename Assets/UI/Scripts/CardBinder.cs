using UnityEngine;
using UnityEngine.UIElements;

namespace LastFreeCity.UI
{
    [RequireComponent(typeof(UIDocument))]
    [ExecuteInEditMode]
    public class CardBinder : MonoBehaviour
    {
        [Header("Data Source")]
        public CardTemplate cardData;

        private UIDocument _uiDocument;
        private VisualElement _root;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            UpdateUI();
        }

        private void Start()
        {
            UpdateUI();
        }

        private void OnValidate()
        {
            // Update automatically when fields are modified in the inspector
            UpdateUI();
        }

        public void BindCard(CardTemplate data)
        {
            cardData = data;
            UpdateUI();
        }

        [ContextMenu("Refresh UI")]
        public void UpdateUI()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null) return;
            
            _root = _uiDocument.rootVisualElement;
            if (_root == null || cardData == null) return;

            // Bind name
            var nameLabel = _root.Q<Label>("card-name");
            if (nameLabel != null) nameLabel.text = cardData.cardName.ToUpper();

            // Bind cost
            var costLabel = _root.Q<Label>("card-cost");
            if (costLabel != null) costLabel.text = cardData.treasuryCost.ToString();

            // Bind type
            var typeLabel = _root.Q<Label>("card-type");
            if (typeLabel != null) typeLabel.text = cardData.cardType.ToString().ToUpper();

            // Bind stats
            var healthLabel = _root.Q<Label>("card-health");
            if (healthLabel != null) healthLabel.text = cardData.health.ToString();

            var attackLabel = _root.Q<Label>("card-attack");
            if (attackLabel != null) attackLabel.text = cardData.attack.ToString();

            var rangeLabel = _root.Q<Label>("card-range");
            if (rangeLabel != null) rangeLabel.text = cardData.range.ToString();

            // Bind ability text
            var abilityLabel = _root.Q<Label>("card-ability");
            if (abilityLabel != null) abilityLabel.text = cardData.abilityText;

            // Bind art texture
            var artElement = _root.Q<VisualElement>("card-art");
            if (artElement != null)
            {
                if (cardData.customArt != null)
                {
                    artElement.style.backgroundImage = new StyleBackground(cardData.customArt);
                }
                else
                {
                    artElement.style.backgroundImage = StyleKeyword.None;
                }
            }
        }
    }
}
