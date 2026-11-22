using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Todo lo que viaja con un golpe. Es un struct para no generar basura en memoria
    /// en cada impacto: se crea, se pasa a TakeDamage y se descarta.
    /// Los IDamageFilter lo reciben por ref y pueden modificarlo antes de aplicarse.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Cantidad de daño antes de filtros (esquiva, reducción, etc.).</summary>
        public float Amount;

        /// <summary>Quién hizo el daño. Puede ser null (daño del entorno).</summary>
        public GameObject Source;

        /// <summary>Bando de quien hizo el daño.</summary>
        public Team SourceTeam;

        /// <summary>Punto del impacto en el mundo, para efectos y números de daño.</summary>
        public Vector3 HitPoint;

        /// <summary>Dirección del golpe (normalizada), para empuje y reacciones.</summary>
        public Vector3 Direction;

        /// <summary>Fuerza de empuje. 0 = no empuja.</summary>
        public float Knockback;

        /// <summary>
        /// Si es true, corta la canción de la bardo y el casteo de la bruja.
        /// Por defecto, todo golpe interrumpe; algunos (veneno, quemadura) no.
        /// </summary>
        public bool Interrupts;

        /// <summary>Golpe especial (tercer golpe del combo, explosión) para efectos más fuertes.</summary>
        public bool IsHeavy;

        /// <summary>
        /// Golpe silencioso: se aplica y muestra su número, pero sin pausa de impacto ni sacudida de cámara.
        /// Lo usan los ataques a distancia de los compañeros (la Nota de la bardo), para que no le congelen
        /// el juego al jugador cada vez que aciertan. Por defecto es false.
        /// </summary>
        public bool Quiet;

        public static DamageInfo Create(float amount, GameObject source, Team sourceTeam,
                                        Vector3 hitPoint, Vector3 direction,
                                        float knockback = 0f, bool interrupts = true, bool isHeavy = false)
        {
            return new DamageInfo
            {
                Amount = amount,
                Source = source,
                SourceTeam = sourceTeam,
                HitPoint = hitPoint,
                Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero,
                Knockback = knockback,
                Interrupts = interrupts,
                IsHeavy = isHeavy
            };
        }

        public override string ToString()
        {
            string origen = Source != null ? Source.name : "entorno";
            return $"{Amount:0.#} de {origen} ({SourceTeam}){(Interrupts ? " [interrumpe]" : "")}{(IsHeavy ? " [pesado]" : "")}";
        }
    }
}
