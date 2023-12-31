using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static BattleStateMachine;
using Unity.VisualScripting;

public class EnemyStateMachine : MonoBehaviour
{
    
    public BaseEnemy enemy;

    [System.Serializable]
    public class SkillSet
    {
        public BaseAttack possibleSkill;
        [Range(0,100)]public int skillSpawnChance = 25;
    }

    public class PassiveSkillSet
    {
        public PassiveSkill posPassive;
        [Range(0, 100)] public int skillSpawnChance = 25;
    }

    public List<SkillSet> PossibleSkills = new List<SkillSet>();
    public List<PassiveSkillSet> PossiblePassives = new List<PassiveSkillSet>();

    //public List<ScriptableObject> Skills = new List<ScriptableObject>();

    private BattleStateMachine BSM;
    //private BaseHero hero;

    public enum TurnState
    {
        PROCESSING,
        CHOOSEACTION,
        WAITING,
        ACTION,
        DEAD
    }

    public TurnState currentState;

    private Vector3 startposition;
    //timeforaction
    private bool actionStarted = false;
    public GameObject HeroToAttack;
    private float animSpeed = 15f;
    public GameObject Selector;
    //enemy panel
    //private EnemyPanelStats stats;
    //public GameObject EnemyPanel;
    //private Transform EnemyPanelSpacer;
    public HealthBar healthBar;
    public GameObject FloatingText;
    [SerializeField] private GameObject MagicVFX;
    public GameObject HealVFX;
    public GameObject RessurectVFX;

    private bool isMelee;
    public bool secondAttackRunning = false;
    public bool counterAttack = false;
    private float hitChance;

    private Animator enemyAnim;
    private AudioSource enemyAudio;

    private bool isCriticalE = false;
    
    //For testing purpouses
    public bool doubleHit;
    private bool attackTwice = false;
    private int killStreak = 0;

    //alive
    private bool alive = true;

    private void Awake()
    {
        SetParams();
        PopulateSkilllist();
        doubleHit = true;
    }

    void Start()
    {
        TMP_Text enemyName = enemy.displayNameText;
        enemyName.text = enemy.theName.ToString();
        //find spacer object
        //EnemyPanelSpacer = GameObject.Find("BattleCanvas").transform.Find("EnemyPanel").transform.Find("EnemyPanelSpacer");

        //create panel and fill in info
        //CreateEnemyPanel();
        currentState = TurnState.PROCESSING;
        Selector.SetActive (false);
        BSM = GameObject.Find("BattleManager").GetComponent<BattleStateMachine> ();
        startposition = transform.position;

        enemyAnim = GetComponent<Animator>();
        enemyAudio = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case (TurnState.PROCESSING):
                if(BSM.battleStates == PerformAction.IDLE)
                {
                    currentState = TurnState.CHOOSEACTION;
                }
                break;

            case (TurnState.CHOOSEACTION):
                ChooseAction();
                currentState = TurnState.WAITING;
                break;

            case (TurnState.WAITING):
                //idle state
                break;

            case (TurnState.ACTION):
                StartCoroutine(TimeForAction());
                break;

            case (TurnState.DEAD):
                if(!alive)
                {
                    return;
                }
                else
                {
                    //change tag of enemy
                    gameObject.tag = "DeadEnemy";
                    //not attackable by heroes
                    BSM.EnemiesInBattle.Remove(gameObject);
                    //disable the selector
                    Selector.SetActive (false);
                    healthBar.gameObject.SetActive(false);
                    //remove all inputs
                    if (BSM.EnemiesInBattle.Count > 0)
                    {
                        for (int i = 0; i < BSM.PerformList.Count; i++)
                        {
                            if (i != 0)
                            {
                                if (BSM.PerformList[i].AttackersGameObject == gameObject)
                                {
                                    BSM.PerformList.Remove(BSM.PerformList[i]);
                                }
                                //if someone has them as a target and it's single target atttack, we will replace it to someone random
                                else if (BSM.PerformList[i].AttackersTarget[0] == gameObject && BSM.PerformList[i].AttackersTarget.Count == 1) //Look in all the lists of attack targets.
                                {
                                    BSM.PerformList[i].AttackersTarget.Remove(gameObject);
                                    if (BSM.PerformList[i].Type == "Hero")
                                    {
                                        BSM.PerformList[i].AttackersGameObject.GetComponent<HeroStateMachine>().EnemyToAttack.Remove(gameObject);
                                    }
                                    BSM.PerformList[i].AttackersTarget.Add(BSM.EnemiesInBattle[Random.Range(0, BSM.EnemiesInBattle.Count)]);
                                }
                                else
                                {
                                    for (int j = 0; j < BSM.PerformList[i].AttackersTarget.Count; j++)
                                    {
                                        if (gameObject)
                                            BSM.PerformList[i].AttackersTarget.Remove(gameObject);
                                    }
                                }
                            }
                        }
                        //else tons load of shit is to do here:
                        //Check for total alive enemies in battle,
                        //make a list from them
                        //add to new list different random targets
                        //

                    }
                    //change the color to gray / play death animation
                    gameObject.GetComponent<SpriteRenderer>().color = new Color32(61, 61, 61, 255);
                    //make not alive
                    alive = false;
                    //fade out and make not active
                    StartCoroutine(FadeOut());
                    //reset enemy buttons
                    BSM.EnemyButtons();
                    //check alive
                    BSM.battleStates = PerformAction.CHECKALIVE;
                }
            break;
        }
    }

    private IEnumerator FadeOut()
    {
        SpriteRenderer rend;
        rend = GetComponent<SpriteRenderer>();
        for (float f = 1f; f >= -0.02f; f -= 0.02f)
        {
            Color c = rend.material.color;
            c.a = f;
            rend.material.color = c;
            yield return new WaitForSeconds(0.02f);
        }
        if(BSM.EnemiesInBattle.Count > 0)
            gameObject.SetActive(false);
    }

    void ChooseAction()
    {
        HandleTurn myAttack = new HandleTurn();
        myAttack.Attacker = enemy.theName;
        myAttack.attackersSpeed = enemy.curSpeed;
        myAttack.Type = "Enemy";
        myAttack.AttackersGameObject = gameObject;
        //Target choice: Randomly choose the target from list. Editable for later.
        myAttack.AttackersTarget.Add(BSM.HeroesInBattle[Random.Range(0, BSM.HeroesInBattle.Count)]);
        
        int num = Random.Range(0, enemy.attacks.Count);
        myAttack.choosenAttack = enemy.attacks[num];

        if (myAttack.choosenAttack.attackType != "Melee")
        {
            isMelee = false;
        }
        else
        {
            isMelee = true;
        }

        BSM.CollectActions(myAttack);
    }


    private IEnumerator TimeForAction()
    {
        if (actionStarted)
        {
            yield break;
        }

        actionStarted = true;
        attackTwice = false;
        secondAttackRunning = false;

        //animate the enemy near the hero to attack

        if (isMelee)
        {
            Vector3 heroPosition = new Vector3(HeroToAttack.transform.position.x-0.6f, HeroToAttack.transform.position.y+0.3f /*, HeroToAttack.transform.position.z */);
            while (MoveTowardsEnemy(heroPosition))
            {
                yield return null;
            }
        }
        //wait a bit till animation of attack plays. Might wanna change later on based on animation.
        yield return new WaitForSeconds(0.25f);

        enemyAnim.Play("Attack");
        enemyAudio.Play();

        yield return new WaitForSeconds(0.7f);
        DoDamage();
        yield return new WaitForSeconds(0.25f);
        //check for counterattack
        if (BSM.PerformList[0].AttackersTarget[0].GetComponent<HeroStateMachine>().counterAttack == true)
        {
            yield return new WaitForSeconds(1.0f);
        }

        //Double Hit mechanic testing
        //If target died from first attack, do not attack for the second time
        //If we intend to attack, it has 35% chance to do so
        if (Random.Range(0, 100) < GameManager.instance.doubleAttackChance && enemy.curHP > 0)
        {
            attackTwice = true;
        }

        if (doubleHit && HeroToAttack.GetComponent<HeroStateMachine>().hero.curHP > 0 && attackTwice == true)
        {
            if (HeroToAttack.GetComponent<HeroStateMachine>().dodgedAtt == false)
            {
                secondAttackRunning = true;
                enemyAnim.Play("Attack");
                enemyAudio.Play();
                DoDamage();
                yield return new WaitForSeconds(0.7f);
            }
        }

        //testing kill streak mechanics
        //after killing one target the killer should choose next one and attack it and do it untill he can't kill the next target
        if (HeroToAttack.GetComponent<HeroStateMachine>().hero.curHP <= 0)
        {
            killStreak++;
            Debug.Log("Kill Streak = " + killStreak);
        }
              
        if (isMelee && enemy.curHP > 0)
        {
            //animate back to start position
            Vector3 firstPosition = startposition;
            while (MoveTowardsStart(firstPosition))
            {
                yield return null;
            }
        }
        //remove this performer from the list in BSM

        BSM.PerformList.RemoveAt(0);
        //reset the battle state machine -> set to wait
        BSM.battleStates = PerformAction.START;

        //BSM.battleStates = BattleStateMachine.PerformAction.START;
        //end coroutine
        actionStarted = false;
        //reset this enemy state
        //cur_cooldown = 0f;
        if(enemy.curHP > 0 && currentState != TurnState.DEAD)
        {
            currentState = TurnState.PROCESSING;
        }


    }

    //Move sprite towards target
    private bool MoveTowardsEnemy(Vector3 target)
    {
        return target != (transform.position = Vector3.MoveTowards(transform.position, target, animSpeed * Time.deltaTime));
    }

    //return the sprite towards starting position on battlefield
    private bool MoveTowardsStart(Vector3 target)
    {
        return target != (transform.position = Vector3.MoveTowards(transform.position, target, animSpeed * Time.deltaTime));
    }

    void DoDamage()
    {
        //dodge / hit calculations for all attack types (magic perfect hit to be added later)
        //get enemy hit value get target dodge value
        //hit / dodge x 100 = chance to hit call it hitChance
        //if hit > dodge, just proceed if hit < dodge random range (1, 100) < hitChance =  
        //
                //do damage
        float minMaxAtk = Mathf.Round(Random.Range(enemy.minATK, enemy.maxATK));
        //float calc_damage = enemy.curATK + BSM.PerformList[0].choosenAttack.attackDamage;
        float calc_damage = minMaxAtk + BSM.PerformList[0].choosenAttack.attackDamage;
        //critical strikes
        if (Random.Range(0, 100) <= enemy.curCRIT)
        {
            isCriticalE = true;
            calc_damage = Mathf.Round(calc_damage * enemy.critDamage);
        }

        //add damage formula later on
        float opponentDef = HeroToAttack.GetComponent<HeroStateMachine>().hero.curDEF;
        calc_damage -= opponentDef;
        if (calc_damage < 0)
        {
            calc_damage = 0;
        }

        HeroToAttack.GetComponent<HeroStateMachine>().TakeDamage(calc_damage, isCriticalE, enemy.curHit, isMelee, false);
        if (HeroToAttack.GetComponent<HeroStateMachine>().dodgedAtt == false)
        {
            RestoreHP(calc_damage, 30); //testing vampirism and restore HP. How much we should heal and how much %% from this.
        }
    
        isCriticalE = false;
    }

    public void TakeDamage(float getDamageAmount, bool isCriticalH, float heroHit, bool isDodgeable, bool isCounterAttack)
    {
        //Calculate if the attack hits
        hitChance = (heroHit / enemy.curDodge) * 100; //(80 / 100) * 100 = 80%    (200 / 100) * 100 = 200
        if (isDodgeable == false)
        {
            hitChance = 100;
        }
        if (Random.Range(0, 101) <= hitChance)
        {
            AttackEffectPlay();
            enemyAnim.Play("Hurt");

            enemy.curHP -= getDamageAmount;
            if (enemy.curHP <= 0)
            {
                enemy.curHP = 0;
                //passive ressurect skill
                SelfRessurect(GameManager.instance.selfRessurrectChance, 50);
            }

            //show popup damage
            DamagePopup(isCriticalH, getDamageAmount, false); //is critical? how many damage? is it heal?

            //update health bar
            healthBar.SetSize(((enemy.curHP * 100) / enemy.baseHP) / 100);
        }
        else
        {
            DodgePopup();
        }


        if (isDodgeable == true && Random.Range(0, 100) <= 100 && !isCounterAttack && enemy.curHP > 0)
        {
            StartCoroutine(CounterAttack());
        }
        

        //UpdateEnemyPanel();
    }

   
    private IEnumerator CounterAttack()
    {
        if (counterAttack)
        {
            yield break;
        }

        counterAttack = true;

        yield return new WaitForSeconds(0.5f);
        enemyAnim.Play("Attack");
        enemyAudio.Play();
        yield return new WaitForSeconds(0.25f);
        float minMaxAtk = Mathf.Round(Random.Range(enemy.minATK, enemy.maxATK));

        if (Random.Range(0, 100) <= enemy.curCRIT)
        {
            isCriticalE = true;
            minMaxAtk = Mathf.Round(minMaxAtk * enemy.critDamage);
        }
        BSM.PerformList[0].AttackersGameObject.GetComponent<HeroStateMachine>().TakeDamage(minMaxAtk, isCriticalE, enemy.curHit, true, counterAttack);
        yield return new WaitForSeconds(0.5f);
        counterAttack = false;
    }

    private void AttackEffectPlay()
    {
        MagicVFX = BSM.GetComponent<BattleStateMachine>().PerformList[0].choosenAttack.attackVFX;
        if (MagicVFX != null)
        {
            var vfx = Instantiate(MagicVFX, transform.position, Quaternion.identity, transform);
        }

    }

    //void CreateEnemyPanel()
    //{
    //    EnemyPanel = Instantiate(EnemyPanel) as GameObject;
    //    stats = EnemyPanel.GetComponent<EnemyPanelStats>();
    //    stats.EnemyName.text = enemy.theName;
    //    stats.EnemyHP.text = "HP: " + enemy.curHP + "/" + enemy.baseHP;
    //    stats.EnemyMP.text = "MP: " + enemy.curMP + "/" + enemy.baseMP;

    //    //ProgressBar = stats.ProgressBar;
    //    EnemyPanel.transform.SetParent(EnemyPanelSpacer, false);
    //}

    //update visual stats upon taking damage / heal
    //void UpdateEnemyPanel()
    //{
    //    stats.EnemyHP.text = "HP: " + enemy.curHP + "/" + enemy.baseHP;
    //    stats.EnemyMP.text = "MP: " + enemy.curMP + "/" + enemy.baseMP;
    //}

    private void SetParams()
    {
        //for randomness

        enemy.strength = Random.Range(15, 25);
        enemy.intellect = Random.Range(15, 25);
        enemy.dexterity = Random.Range(15, 25);
        enemy.agility = Random.Range(15, 35);
        enemy.stamina = Random.Range(15, 25);

        //Calculate HP based on Stats
        enemy.baseHP = Mathf.Round(enemy.strength * enemy.hpPerStr) + (enemy.stamina * enemy.hpPerSta);
        enemy.curHP = enemy.baseHP;

        //Calculate MP based on stats
        enemy.baseMP = Mathf.Round(enemy.intellect * enemy.mpPerInt);
        enemy.curMP = enemy.baseMP;

        //Calculate Attack based on stats
        enemy.baseATK = Mathf.Round((enemy.strength * enemy.atkPerStr) + (enemy.intellect * enemy.atkPerInt));
        enemy.curATK = enemy.baseATK;

        enemy.maxATK = enemy.baseATK + Random.Range(10, 50);
        enemy.minATK = enemy.baseATK;

        //Calculate HIT based on stats
        enemy.baseHit = Mathf.Round(enemy.dexterity * enemy.hitPerDex);
        enemy.curHit = enemy.baseHit;

        //Calculate dodge based on stats
        enemy.baseDodge = Mathf.Round(enemy.agility * enemy.dodgePerAgi);
        enemy.curDodge = enemy.baseDodge;

        //calculate def based on stats
        enemy.baseDEF = Mathf.Round(enemy.stamina * enemy.defPerSta);
        enemy.curDEF = enemy.baseDEF;

        //calculate critrate based on stats
        //enemy.curCRIT = enemy.baseCRIT;

        //calculate speed based on stats
        enemy.baseSpeed = Mathf.Round(enemy.agility * enemy.spdPerAgi);
        enemy.curSpeed = enemy.baseSpeed;

        enemy.expAmount = enemy.strength + enemy.intellect + enemy.dexterity + enemy.agility + enemy.stamina;
    }

    void PopulateSkilllist() //Take skills from the list of possible skills, calculate chances and spawn in according lists
    {
        for (int i = 0; i < PossibleSkills.Count; i++)
        {
            if(Random.Range(0, 100) < PossibleSkills[i].skillSpawnChance)
            {
                BaseAttack NewSkill = PossibleSkills[i].possibleSkill;
                //Debug.Log(NewSkill.attackType);
                if (NewSkill.attackType == "Spell")
                {
                    enemy.MagicAttacks.Add(NewSkill);
                }
                else
                {
                    enemy.attacks.Add(NewSkill);
                }
            }
        }
    }

    void DodgePopup()
    {
        enemyAnim.Play("Hurt"); // replace with step away animation later
        var go = Instantiate(FloatingText, transform.position, Quaternion.identity, transform); //tell that we dodged and no damage is dealt
        go.GetComponentInChildren<SpriteRenderer>().enabled = false;
        go.GetComponent<TextMeshPro>().fontSize = 2;
        go.GetComponent<TextMeshPro>().color = Color.white;
        go.GetComponent<TextMeshPro>().text = "DODGE";
    }


    private void DamagePopup(bool isCritical, float DamageAmount, bool isHeal)
    {
        var go = Instantiate(FloatingText, transform.position, Quaternion.identity, transform);
        if (isCritical == true)
        {   
            go.GetComponentInChildren<SpriteRenderer>().enabled = true;
            go.GetComponent<TextMeshPro>().fontSize = 5;
            go.GetComponent<TextMeshPro>().color = Color.red;
        }

        else if (isHeal)
        {
            go.GetComponentInChildren<SpriteRenderer>().enabled = false;
            go.GetComponent<TextMeshPro>().color = Color.green;
        }

        else
        {
            go.GetComponentInChildren<SpriteRenderer>().enabled = false;
            go.GetComponent<TextMeshPro>().color = new Color32(197, 164, 0, 255);
        }

        go.GetComponent<TextMeshPro>().text = DamageAmount.ToString();
    }

    void ResPopup(float ResHP)
    {
        var go2 = Instantiate(FloatingText, transform.position, Quaternion.identity, transform);
        go2.GetComponentInChildren<SpriteRenderer>().enabled = false;
        go2.GetComponent<TextMeshPro>().color = Color.green;
        go2.GetComponent<TextMeshPro>().text = ResHP.ToString();
    }

    public void RestoreHP(float damage, float percentage)
    {
        float vampAmount = Mathf.Round((damage * percentage) / 100);
        enemy.curHP += vampAmount;
        if(enemy.curHP > enemy.baseHP)
        {
            enemy.curHP = enemy.baseHP;
        }
        DamagePopup(false, vampAmount, true);
        Instantiate(HealVFX, transform.position, Quaternion.identity, transform);
        healthBar.SetSize(((enemy.curHP * 100) / enemy.baseHP) / 100);
    }

    public void SelfRessurect(int resChance, int resHP)
    {
        if (Random.Range(0, 100) <= resChance)
        {
            enemy.curHP = Mathf.Round((enemy.baseHP / 100) * resHP);
            ResPopup(enemy.curHP);
            Instantiate(RessurectVFX, transform.position, Quaternion.identity, transform);
        }
        else
        {
            enemyAnim.Play("Die");
            currentState = TurnState.DEAD;
        }
    }


    //Undead mechanic
    //Perk1: While enemy with undead dies, it will rise after X turns with X HP;
    //Drawback: Undead takes 2x damage from holy property / Exorcism passive skill and if killed by holy/exorcism, can't rise
    //Drawback / perk2: Can't be buffed or debuffed / healed neither by enemies or allies unless the skill level isn't maximal
    //If undead: after death remove from PerformList, but don't remove from enemies in Battle so it can be targeted by players
    //If is targeted by player, but hasn't risen at the start of the turn, then switch targets.
    //At this point adding the rise method
    //All those skill related methods with later be moved to corresponding places, at this point it all is for testing purposes.

    public void UndeadRise()
    {
        alive = true;
        gameObject.tag = "Enemy";
        Selector.SetActive(true);
        ChooseAction();
    }

}

