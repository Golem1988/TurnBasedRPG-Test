using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BaseAttack : MonoBehaviour
{
    public string attackName;
    public string attackDescription;
    public float attackDamage; //calculate the end damage
    public float attackCost; //if it's a spell, requires MP


}
