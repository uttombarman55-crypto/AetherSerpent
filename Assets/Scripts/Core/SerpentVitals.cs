using System;
using UnityEngine;

namespace AetherSerpent.Core
{
    /// <summary>
    /// Governs core biological vitals: Health, Hunger (Metabolism), and Energy (Stamina).
    /// Features:
    /// - Continuous hunger depletion (metabolic burn)
    /// - Low hunger penalty (stamina recovery slows down, health begins decaying)
    /// - High vitality buffs (faster regeneration and increased agility)
    /// - Death dispatch with full recovery and respawn flow
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class SerpentVitals : MonoBehaviour
    {
        public static SerpentVitals Instance { get; private set; }

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float healthRegenRate = 2.5f;

        [Header("Hunger / Metabolism")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float hungerDepletionRate = 1.8f; // Burn rate per second
        [SerializeField] private float starvationDamageRate = 4.0f; // Damage per second when starving

        [Header("Energy / Stamina")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float energyRegenRate = 20f;
        [SerializeField] private float boostCostPerSecond = 30f;

        // Public Readouts (0.0 to 1.0 Normalized)
        public float HealthPercent => Mathf.Clamp01(currentHealth / maxHealth);
        public float HungerPercent => Mathf.Clamp01(currentHunger / maxHunger);
        public float EnergyPercent => Mathf.Clamp01(currentEnergy / maxEnergy);
        public float CurrentHealth => currentHealth;
        public float CurrentHunger => currentHunger;
        public float CurrentEnergy => currentEnergy;

        public bool IsDead { get; private set; } = false;
        public bool IsStarving => currentHunger <= 0.05f;

        // Events
        public event Action<float, float, float> OnVitalsUpdated; // health, hunger, energy
        public event Action OnDeathEvent;
        public event Action OnRespawnEvent;

        private float currentHealth;
        private float currentHunger;
        private float currentEnergy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            currentHealth = maxHealth;
            currentHunger = maxHunger;
            currentEnergy = maxEnergy;
        }

        private void Update()
        {
            if (IsDead) return;

            ProcessMetabolism();
            ProcessRegeneration();
            OnVitalsUpdated?.Invoke(HealthPercent, HungerPercent, EnergyPercent);
        }

        private void ProcessMetabolism()
        {
            // Hunger continuously burns down over time
            currentHunger = Mathf.Max(0f, currentHunger - (hungerDepletionRate * Time.deltaTime));

            // Starvation begins taking toll if completely depleted
            if (currentHunger <= 0.001f)
            {
                TakeDamage(starvationDamageRate * Time.deltaTime, "Starvation");
            }
        }

        private void ProcessRegeneration()
        {
            // Energy naturally recovers unless starving
            float activeEnergyRate = IsStarving ? energyRegenRate * 0.35f : energyRegenRate;
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + (activeEnergyRate * Time.deltaTime));

            // Health regenerates if well-fed (> 50% hunger)
            if (currentHunger > 50f && currentHealth < maxHealth)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + (healthRegenRate * Time.deltaTime));
            }
        }

        public bool ConsumeEnergy(float amount)
        {
            if (currentEnergy >= amount)
            {
                currentEnergy = Mathf.Max(0f, currentEnergy - amount);
                return true;
            }
            return false;
        }

        public void ConsumeBoost(float deltaTime)
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - (boostCostPerSecond * deltaTime));
        }

        public void Feed(float nutritionAmount, float energyBoost = 15f)
        {
            if (IsDead) return;

            currentHunger = Mathf.Min(maxHunger, currentHunger + nutritionAmount);
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyBoost);
            currentHealth = Mathf.Min(maxHealth, currentHealth + (nutritionAmount * 0.25f));
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void TakeDamage(float damage, string source = "")
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);

            if (currentHealth <= 0f)
            {
                Die(source);
            }
        }

        private void Die(string source)
        {
            IsDead = true;
            Debug.Log($"[SerpentVitals] The Serpent has fallen. Cause of death: {source}");
            OnDeathEvent?.Invoke();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.GameOver);
            }
        }

        public void Respawn()
        {
            IsDead = false;
            currentHealth = maxHealth;
            currentHunger = maxHunger * 0.85f;
            currentEnergy = maxEnergy;
            OnRespawnEvent?.Invoke();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.Gameplay);
            }
        }
    }
}
