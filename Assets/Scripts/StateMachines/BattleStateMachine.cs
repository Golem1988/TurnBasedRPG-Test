using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System;

public class BattleStateMachine : MonoBehaviour
{
    //    Fight structure:
    //      1) PreBattle
    //      2) BATTLE (loop)
    //          2.0) Input >
    //          2.1) pre-turn >
    //          2.2) turn >
    //              2.2.1) win -> PostBattle
    //              2.2.2) lose -> PostBattle
    //          2.3) post-turn -> Input
    //      3) PostBattle

    public enum BattlePhases
    {
        PREBATTLE,
        PLAYERINPUT,
        PREFIGHT,//starts pre-action coroutine
        FIGHT,
        POSTFIGHT //starts post-fight coroutine
    }

    private bool prebattleStarted = false;
    private bool postbattleStarted = false;
    private bool postFightStarted = false;
    private bool preFightStarted = false;

    public enum PerformAction
    {
        IDLE,
        START,
        TAKEACTION,
        PERFORMACTION,
        CHECKALIVE,
        WIN,
        LOSE
    }

    public PerformAction battleStates;
    public BattlePhases battlePhases;

    public List<HandleTurn> PerformList = new List<HandleTurn>();

    public List<GameObject> HeroesInBattle = new List<GameObject>();
    public List<GameObject> EnemiesInBattle = new List<GameObject>();

    private List<GameObject> sortBySpeed = new List<GameObject>();
    private List<GameObject> sortByHP = new List<GameObject>();

    //Turn things
    public int ChoicesMade = 0;
    public int CurrentTurn = 1;

    private float currentCount = 0f;
    public float startingCount = 30f;

    //autobattle?
    public bool autoBattle = false;
    public int autobattleTurns;

    private bool countdownTrigger = false;


    public enum HeroGUI
    {
        NOTACTIVE,
        ACTIVATE,
        WAITING,
        INPUT1, //select attack
        INPUT2, //select target
        DONE
    }

    public HeroGUI HeroInput;

    public List<GameObject> HeroesToManage = new List<GameObject>();
    private HandleTurn HeroChoice;

    public GameObject enemyButton;
    public GameObject allyButton;
    public Transform Spacer;
    public Transform allySpacer;

    public GameObject AttackPanel;
    public GameObject EnemySelectPanel;
    public GameObject AllySelectPanel;
    public GameObject MagicPanel;
    public GameObject AutoBattlePanel;

    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject fightText;
    [SerializeField] private Transform battleCanvas;
    [SerializeField] private TMP_Text autoText;


    //magic attack
    public Transform actionSpacer;
    public Transform magicSpacer;
    public GameObject actionButton;
    public GameObject magicButton;
    private List<GameObject> atkBtns = new List<GameObject>();

    //enemy buttons
    private List<GameObject> enemyBtns = new List<GameObject>();
    //ally buttons
    private List<GameObject> allyBtns = new List<GameObject>();


    //spawnpoints
    public List<Transform> spawnPoints = new List<Transform>();
    public List<Transform> heroSpawnPoints = new List<Transform>();

    //exp calculation for won battle
    private int expMultiplier = 1; //global exp multiplier (in case of world exp increase events)
    public int expTotal; //sum of total enemy exp
    private int getExp; //how much exp will each hero get
    private int xLevel; //sum of enemy levels in battle
    private int averageEnemyLvl;

    //timescale
    private float fixedDeltaTime;
    private float timeModifier;

    void Awake()
    {
        timeModifier = GameManager.instance.fightSpeed;
        SpawnActors();
        AutobattleSetup();
        this.fixedDeltaTime = Time.fixedDeltaTime;
    }

    void Start()
    {
        getExp = 0;
        CollectExp(); //make a sum of exp enemies will be giving in case of victory
        //Set starting enums to idle etc
        HeroInput = HeroGUI.NOTACTIVE;
        battleStates = PerformAction.IDLE;
        battlePhases = BattlePhases.PREBATTLE;
        //countdown set count
        currentCount = startingCount;



        AttackPanel.SetActive(false);
        EnemySelectPanel.SetActive(false);
        MagicPanel.SetActive(false);

        EnemyButtons();
        AllyButtons();
    }

    // Update is called once per frame
    void Update()
    {
        switch (battlePhases)
        {
            case (BattlePhases.PREBATTLE):
                StartCoroutine(PreBattleActions());
                break;

            case (BattlePhases.PLAYERINPUT):
                //wait for player input, countdown etc
                //countdownTrigger = false;
                //countdown thingies
                if (currentCount > 1)
                {
                    currentCount -= 1 * Time.deltaTime;
                    countdownText.text = currentCount.ToString("0");
                    turnText.text = "Upcoming Turn: " + CurrentTurn;
                }
                else
                {
                    countdownText.text = "";
                    countdownTrigger = true;
                }

                if (ChoicesMade >= HeroesInBattle.Count)
                {
                    countdownText.text = "";
                    battlePhases = BattlePhases.PREFIGHT;
                }
                break;

            case (BattlePhases.PREFIGHT):
                StartCoroutine(PreFightActions());
                break;
            case (BattlePhases.FIGHT):
                //idle and show that there's current turn running
                break;

            case (BattlePhases.POSTFIGHT):
                StartCoroutine(PostFightActions());
                break;
        }

        switch (battleStates)
        {
            case (PerformAction.IDLE):
                //pre-battle waiting / just idling
                break;
            case (PerformAction.START):
                //Battle things

                if (battlePhases == BattlePhases.FIGHT && PerformList.Count == 0)
                {
                    battleStates = PerformAction.IDLE;
                    battlePhases = BattlePhases.POSTFIGHT;
                }

                if (PerformList.Count >= 1)
                {
                    turnText.text = "Current Turn: " + CurrentTurn;
                    countdownText.text = "";
                    battleStates = PerformAction.TAKEACTION;
                }

                break;

            case (PerformAction.TAKEACTION):
                GameObject performer = GameObject.Find(PerformList[0].Attacker);
                if (PerformList[0].Type == "Enemy")
                {
                    EnemyStateMachine ESM = performer.GetComponent<EnemyStateMachine>();
                    for (int i = 0; i < HeroesInBattle.Count; i++)
                    {
                        if (PerformList[0].AttackersTarget[0] == HeroesInBattle[i])
                        {
                            ESM.HeroToAttack = PerformList[0].AttackersTarget[0];
                            ESM.currentState = EnemyStateMachine.TurnState.ACTION;
                            break;
                        }
                        else
                        {
                            PerformList[0].AttackersTarget[0] = HeroesInBattle[UnityEngine.Random.Range(0, HeroesInBattle.Count)];
                            ESM.HeroToAttack = PerformList[0].AttackersTarget[0];
                            ESM.currentState = EnemyStateMachine.TurnState.ACTION;
                        }
                    }

                }

                if (PerformList[0].Type == "Hero")
                {
                    HeroStateMachine HSM = performer.GetComponent<HeroStateMachine>();
                    HSM.EnemyToAttack.Clear();
                    for (int i = 0; i < PerformList[0].AttackersTarget.Count; i++)
                    {
                        HSM.EnemyToAttack.Add(PerformList[0].AttackersTarget[i]);
                        //Debug.Log("Added " + PerformList[0].AttackersTarget[i].GetComponent<EnemyStateMachine>().enemy.theName + "to the AttackersTarget");
                    }

                    HSM.currentState = HeroStateMachine.TurnState.ACTION;
                }

                battleStates = PerformAction.PERFORMACTION;
                break;

            case (PerformAction.PERFORMACTION):
                //idle
                break;

            case (PerformAction.CHECKALIVE):
                if (HeroesInBattle.Count < 1)
                {
                    battleStates = PerformAction.LOSE;
                    //lose battle
                }
                else if (EnemiesInBattle.Count < 1)
                {
                    battleStates = PerformAction.WIN;
                    //win battle
                }
                else
                {
                    //call function
                    clearAttackPanel();
                    //battlePhases = BattlePhases.PLAYERINPUT;
                    //HeroInput = HeroGUI.ACTIVATE;
                    battleStates = PerformAction.PERFORMACTION;
                }
                break;

            case (PerformAction.LOSE):
                StartCoroutine(PostBattleActions(false));
                break;

            case (PerformAction.WIN):
                StartCoroutine(PostBattleActions(true));
                break;

        }

        switch (HeroInput)
        {
            case (HeroGUI.NOTACTIVE):
                //just idle before battle
                break;
            case (HeroGUI.ACTIVATE):
                if (HeroesToManage.Count >= 1 && ChoicesMade < HeroesInBattle.Count)
                {
                    HeroesToManage[0].transform.Find("Selector").gameObject.SetActive(true);
                    HeroChoice = new HandleTurn();

                    HeroInput = HeroGUI.WAITING;
                    if (autoBattle == true || countdownTrigger == true)
                    {
                        AutoSelect();
                    }
                    else
                    {
                        AttackPanel.SetActive(true);
                        //populate action buttons
                        CreateAttackButtons();
                    }
                }
                break;
            case (HeroGUI.WAITING):
                //waiting for input
                if (countdownTrigger == true)
                {
                    AutoSelect();
                }
                break;
            case (HeroGUI.DONE):
                HeroInputDone();
                break;
        }

        if (battlePhases != BattlePhases.PLAYERINPUT && battleStates != PerformAction.WIN && battleStates != PerformAction.LOSE)
        {
            Time.timeScale = timeModifier;
            Time.fixedDeltaTime = this.fixedDeltaTime * Time.timeScale;
        }


    }

    public void CollectActions(HandleTurn input)
    {
        PerformList.Add(input);
    }

    public void EnemyButtons()
    {
        //cleanup
        foreach (GameObject enemyBtn in enemyBtns)
        {
            enemyBtn.GetComponent<EnemySelectButton>().EnemyPrefab.transform.Find("Selector").gameObject.SetActive(false);
            Destroy(enemyBtn);
        }
        enemyBtns.Clear();
        //create buttons for each enemy
        foreach (GameObject enemy in EnemiesInBattle)
        {
            GameObject newButton = Instantiate(enemyButton) as GameObject;
            EnemySelectButton button = newButton.GetComponent<EnemySelectButton>();

            EnemyStateMachine cur_enemy = enemy.GetComponent<EnemyStateMachine>();

            Text buttonText = newButton.transform.Find("Text").gameObject.GetComponent<Text>();
            buttonText.text = cur_enemy.enemy.theName;

            button.EnemyPrefab = enemy;

            newButton.transform.SetParent(Spacer, false);
            enemyBtns.Add(newButton);
        }
    }

    public void AllyButtons()
    {
        //cleanup
        foreach (GameObject allyBtn in allyBtns)
        {
            allyBtn.GetComponent<AllySelectButton>().AllyPrefab.transform.Find("Selector").gameObject.SetActive(false);
            Destroy(allyBtn);
        }
        allyBtns.Clear();
        //create buttons for each enemy
        foreach (GameObject ally in HeroesInBattle)
        {
            GameObject newButton = Instantiate(allyButton) as GameObject;
            AllySelectButton button = newButton.GetComponent<AllySelectButton>();

            HeroStateMachine cur_ally = ally.GetComponent<HeroStateMachine>();

            Text buttonText = newButton.transform.Find("Text").gameObject.GetComponent<Text>();
            buttonText.text = cur_ally.hero.theName;

            button.AllyPrefab = ally;

            newButton.transform.SetParent(allySpacer, false);
            allyBtns.Add(newButton);
        }
    }

    public void AutoSelect()
    {
        //HeroChoise = new HandleTurn();
        HeroChoice.Attacker = HeroesToManage[0].name; //might be changed
        HeroChoice.attackersSpeed = HeroesToManage[0].GetComponent<HeroStateMachine>().hero.curSpeed;
        HeroChoice.AttackersGameObject = HeroesToManage[0];
        HeroChoice.Type = "Hero";
        int num = UnityEngine.Random.Range(0, HeroesToManage[0].GetComponent<HeroStateMachine>().hero.attacks.Count);
        HeroChoice.choosenAttack = HeroesToManage[0].GetComponent<HeroStateMachine>().hero.attacks[num];
        HeroChoice.AttackersTarget.Add(EnemiesInBattle[UnityEngine.Random.Range(0, EnemiesInBattle.Count)]);
        HeroInput = HeroGUI.DONE;
    }

    public void Input1() //attack button
    {
        HeroChoice.Attacker = HeroesToManage[0].name; //might be changed
        HeroChoice.attackersSpeed = HeroesToManage[0].GetComponent<HeroStateMachine>().hero.curSpeed;
        HeroChoice.AttackersGameObject = HeroesToManage[0];
        HeroChoice.Type = "Hero";
        HeroChoice.choosenAttack = HeroesToManage[0].GetComponent<HeroStateMachine>().hero.attacks[0];
        AttackPanel.SetActive(false);
        EnemySelectPanel.SetActive(true);
    }


    public void Input2(GameObject choosenEnemy) //select enemy / target
    {
        HeroChoice.AttackersTarget.Add(choosenEnemy); //add initial target to list
        if (HeroChoice.choosenAttack.attackTargets > 1 && EnemiesInBattle.Count >= HeroChoice.choosenAttack.attackTargets)
        {
            sortBySpeed = new List<GameObject>();
            //add all enemies to the list 
            foreach (GameObject en in EnemiesInBattle)
            {
                sortBySpeed.Add(en);
            }
            //remove enemy that already is in the list by default
            sortBySpeed.Remove(choosenEnemy);
            //sort enemies in the list by the speed, then reverse, so we attack enemies with the highest speed
            sortBySpeed = sortBySpeed.OrderBy(x => x.GetComponent<EnemyStateMachine>().enemy.curSpeed).ToList();
            sortBySpeed.Reverse();
            //add speedy enemies to the list
            for (int i = 0; i < (HeroChoice.choosenAttack.attackTargets - 1); i++)
            {
                HeroChoice.AttackersTarget.Add(sortBySpeed[i]);
            }

        }
        else if (HeroChoice.choosenAttack.attackTargets > EnemiesInBattle.Count)
        {
            HeroChoice.AttackersTarget.Remove(choosenEnemy);
            for (int i = 0; i < EnemiesInBattle.Count; i++)
            {
                HeroChoice.AttackersTarget.Add(EnemiesInBattle[i]);
            }
        }
        //else if(HeroChoise.choosenAttack.attackTargets >= EnemiesInBattle.Count)
        //{ 
        //        randomEnemies = new List<GameObject>();
        //        foreach (GameObject en in EnemiesInBattle)
        //        {
        //            randomEnemies.Add(en);
        //        }
        //        if (randomEnemies.Count > 1)
        //        {
        //            randomEnemies.Remove(choosenEnemy);
        //        }

        //        var randomList = GetRandomElements(randomEnemies, HeroChoise.choosenAttack.attackTargets - 1);

        //        foreach (GameObject newEnemy in randomList)
        //        {
        //            HeroChoise.AttackersTarget.Add(newEnemy);
        //        }

        //}

        HeroInput = HeroGUI.DONE;
    }

    public void Input6(GameObject choosenAlly) //select enemy / target
    {
        HeroChoice.AttackersTarget.Add(choosenAlly); //add initial target to list
        if (HeroChoice.choosenAttack.attackTargets > 1 && HeroesInBattle.Count >= HeroChoice.choosenAttack.attackTargets)
        {
            sortByHP = new List<GameObject>();
            //add all enemies to the list 
            foreach (GameObject al in HeroesInBattle)
            {
                sortByHP.Add(al);
            }
            //remove enemy that already is in the list by default
            sortByHP.Remove(choosenAlly);
            //sort enemies in the list by the speed, then reverse, so we attack enemies with the highest speed
            sortByHP = sortByHP.OrderBy(x => x.GetComponent<HeroStateMachine>().hero.curHP).ToList();
            sortByHP.Reverse();
            //add speedy enemies to the list
            for (int i = 0; i < (HeroChoice.choosenAttack.attackTargets - 1); i++)
            {
                HeroChoice.AttackersTarget.Add(sortByHP[i]);
            }

        }
        else if (HeroChoice.choosenAttack.attackTargets > HeroesInBattle.Count)
        {
            HeroChoice.AttackersTarget.Remove(choosenAlly);
            for (int i = 0; i < HeroesInBattle.Count; i++)
            {
                HeroChoice.AttackersTarget.Add(HeroesInBattle[i]);
            }
        }
        HeroInput = HeroGUI.DONE;
    }

    public void Input7()
    {
        //change that later on because this is not how it should actually work.
    }

    void HeroInputDone()
    {
        PerformList.Add(HeroChoice);
        ChoicesMade++;
        //cleanup attack panel
        clearAttackPanel();

        HeroesToManage[0].transform.Find("Selector").gameObject.SetActive(false);
        HeroesToManage.RemoveAt(0);
        HeroInput = HeroGUI.ACTIVATE;
    }

    void clearAttackPanel()
    {
        EnemySelectPanel.SetActive(false);
        AllySelectPanel.SetActive(false);
        AttackPanel.SetActive(false);
        MagicPanel.SetActive(false);

        foreach (GameObject atkBtn in atkBtns)
        {
            Destroy(atkBtn);
        }
        atkBtns.Clear();
    }

    //create actiobuttons
    void CreateAttackButtons()
    {
        GameObject AttackButton = Instantiate(actionButton) as GameObject;
        Text AttackButtonText = AttackButton.transform.Find("Text").gameObject.GetComponent<Text>();
        AttackButtonText.text = "Attack";
        AttackButton.GetComponent<Button>().onClick.AddListener(() => Input1());
        AttackButton.transform.SetParent(actionSpacer, false);
        atkBtns.Add(AttackButton);

        GameObject MagicAttackButton = Instantiate(actionButton) as GameObject;
        Text MagicAttackButtonText = MagicAttackButton.transform.Find("Text").gameObject.GetComponent<Text>();
        MagicAttackButtonText.text = "Magic";
        MagicAttackButton.GetComponent<Button>().onClick.AddListener(() => Input3());
        MagicAttackButton.transform.SetParent(actionSpacer, false);
        atkBtns.Add(MagicAttackButton);


        //Flee button allowing to flee the battle
        GameObject FleeButton = Instantiate(actionButton) as GameObject;
        Text FleeButtonText = FleeButton.transform.Find("Text").gameObject.GetComponent<Text>();
        FleeButtonText.text = "Flee";
        FleeButton.GetComponent<Button>().onClick.AddListener(() => Input5());
        FleeButton.transform.SetParent(actionSpacer, false);
        atkBtns.Add(FleeButton);

        //Defence button
        //Defence system: If in defence stance, take only XX % damage. Attack noone.
        GameObject DefendButton = Instantiate(actionButton) as GameObject;
        Text DefendButtonText = DefendButton.transform.Find("Text").gameObject.GetComponent<Text>();
        DefendButtonText.text = "Defend";
        DefendButton.GetComponent<Button>().onClick.AddListener(() => Input7());
        DefendButton.transform.SetParent(actionSpacer, false);
        atkBtns.Add(DefendButton);
        DefendButton.GetComponent<Button>().interactable = false;


        //Autobattle enable button
        GameObject AutoSelectButton = Instantiate(actionButton) as GameObject;
        Text AutoSelectButtonText = AutoSelectButton.transform.Find("Text").gameObject.GetComponent<Text>();
        AutoSelectButtonText.text = "Auto";
        AutoSelectButton.GetComponent<Button>().onClick.AddListener(() => ToggleAutoBattle(true));
        AutoSelectButton.transform.SetParent(actionSpacer, false);
        atkBtns.Add(AutoSelectButton);

        if (HeroesToManage[0].GetComponent<HeroStateMachine>().hero.MagicAttacks.Count > 0)
        {
            foreach (BaseAttack magicAtk in HeroesToManage[0].GetComponent<HeroStateMachine>().hero.MagicAttacks)
            {
                GameObject MagicButton = Instantiate(magicButton) as GameObject;
                Text MagicButtonText = MagicButton.transform.Find("Text").gameObject.GetComponent<Text>();
                MagicButtonText.text = magicAtk.attackName;
                AttackButton ATB = MagicButton.GetComponent<AttackButton>();
                ATB.magicAttackToPerform = magicAtk;
                MagicButton.transform.SetParent(magicSpacer, false);
                atkBtns.Add(MagicButton);
            }
        }
        else
        {
            MagicAttackButton.GetComponent<Button>().interactable = false;
        }
    }

    public void Input4(BaseAttack choosenMagic) //chosen magic attack
    {
        HeroChoice.Attacker = HeroesToManage[0].name; //might be changed
        HeroChoice.attackersSpeed = HeroesToManage[0].GetComponent<HeroStateMachine>().hero.curSpeed;
        HeroChoice.AttackersGameObject = HeroesToManage[0];
        HeroChoice.Type = "Hero";
        HeroChoice.choosenAttack = choosenMagic;
        MagicPanel.SetActive(false);
        if (choosenMagic.isAttack == true)
        {
            EnemySelectPanel.SetActive(true);
        }
        else
        {
            AllySelectPanel.SetActive(true);
        }
    }

    public void Input3() //switching to magic attacks
    {
        AttackPanel.SetActive(false);
        MagicPanel.SetActive(true);
    }
    public void Input5() //fleeing from battle and clearing the current battle information
    {
        for (int i = 0; i < HeroesInBattle.Count; i++)
        {
            HeroesInBattle[i].GetComponent<HeroStateMachine>().currentState = HeroStateMachine.TurnState.WAITING;
        }

        Debug.Log("Fleed from battle");
        GameManager.instance.LoadSceneAfterBattle();
        GameManager.instance.gameState = GameManager.GameStates.WORLD_STATE;
        GameManager.instance.enemysToBattle.Clear();
    }

    public void ToggleAutoBattle(bool once)
    {
        if (once)
        {
            autoBattle = true;
            AutoBattlePanel.SetActive(true);
        }
        else
        {
            autoBattle = !autoBattle;
        }
        if (autoBattle)
        {
            GameManager.instance.autoBattle = true;
            autobattleTurns = GameManager.instance.autoBattleTurns;
            autoText.text = "Autobattle: ON" + Environment.NewLine + "Turns left: " + autobattleTurns;
            if (HeroInput == HeroGUI.WAITING)
            {
                AutoSelect();
            }
        }
        else
        {
            GameManager.instance.autoBattle = false;
            autoText.text = "Autobattle: OFF";
        }
    }

    void InstantiateFightText()
    {
        GameObject FightText = Instantiate(fightText);
        FightText.transform.SetParent(battleCanvas, false);
    }

    void SpawnActors()
    {
        //Put in Start method:
        //EnemiesInBattle.AddRange (GameObject.FindGameObjectsWithTag ("Enemy"));
        //HeroesInBattle.AddRange (GameObject.FindGameObjectsWithTag ("Hero"));

        for (int i = 0; i < GameManager.instance.enemyAmount; i++)
        {
            GameObject NewEnemy = Instantiate(GameManager.instance.enemysToBattle[i], spawnPoints[i].position, Quaternion.identity) as GameObject;
            NewEnemy.name = NewEnemy.GetComponent<EnemyStateMachine>().enemy.theName + " " + (i + 1);
            NewEnemy.GetComponent<EnemyStateMachine>().enemy.theName = NewEnemy.name;
            EnemiesInBattle.Add(NewEnemy);
        }

        for (int i = 0; i < GameManager.instance.heroAmount; i++)
        {
            GameObject NewHero = Instantiate(GameManager.instance.battleHeroes[i], heroSpawnPoints[i].position, Quaternion.identity) as GameObject;
            NewHero.name = NewHero.GetComponent<HeroStateMachine>().hero.theName;
            NewHero.GetComponent<HeroStateMachine>().hero.theName = NewHero.name;
            HeroesInBattle.Add(NewHero);
        }
    }

    void AutobattleSetup()
    {
        autobattleTurns = GameManager.instance.remainingAutobattleTurns;
        if (GameManager.instance.autoBattle == true)
        {
            if (autobattleTurns > 0)
            {
                autoBattle = true;
                AutoBattlePanel.SetActive(true);
                autoText.text = "Autobattle: ON" + Environment.NewLine + "Turns left: " + autobattleTurns;
                Debug.Log("Autobattle is on, remaining autobattle turns: " + autobattleTurns);
            }
        }
        else
        {
            autoText.text = "Autobattle: OFF";
        }
    }

    void AutobattleControl()
    {
        autobattleTurns--;
        GameManager.instance.remainingAutobattleTurns--;
        autoText.text = "Autobattle: ON" + Environment.NewLine + "Turns left: " + autobattleTurns;
        if (autobattleTurns <= 0)
        {
            autoBattle = false;
            autoText.text = "Autobattle: OFF";
        }
    }

    //List<T> GetRandomElements<T>(List<T> inputList, int count)
    //{
    //    List<T> outputList = new List<T>();
    //    for (int i = 0; i < count; i++)
    //    {
    //        int index = UnityEngine.Random.Range(0, inputList.Count);
    //        outputList.Add(inputList[index]);
    //    }
    //    return outputList;
    //}

    //Set up some things that are to be made before battle even begins
    //basically it's just for the pre-battle cooldown purpouse at this point
    private IEnumerator PreBattleActions()
    {
        if (prebattleStarted)
        {
            yield break;
        }

        prebattleStarted = true;

        yield return new WaitForSeconds(GameManager.instance.preFightCooldown);

        //start the fight
        HeroInput = HeroGUI.ACTIVATE;
        battlePhases = BattlePhases.PLAYERINPUT;

        prebattleStarted = false;
    }


    //At this point PreFight makes very little sense actually
    private IEnumerator PreFightActions()
    {
        if (preFightStarted)
        {
            yield break;
        }

        preFightStarted = true;
        Time.timeScale = timeModifier;
        Time.fixedDeltaTime = this.fixedDeltaTime * Time.timeScale;
        InstantiateFightText();
        //Order actors in performlist based on their speed 
        UpdatePerformList();

        yield return new WaitForSeconds(0.5f);

        //apply all the effects that apply just right before the fight ends (like +-HP at the start of the turn)
        if (HeroesInBattle.Count > 0)
        {
            for (int i = 0; i < HeroesInBattle.Count; i++)
            {
                HeroesInBattle[i].GetComponent<HeroStateMachine>().RestoreHP(500, 100); //restore 100% of 500HP for each hero in battle
            }
        }
        //put heroes with special attacks / skills waiting in front of the PerformList
        //(like skills that require activation and will perform first at the start of the next turn)

        yield return new WaitForSeconds(0.5f);

        currentCount = startingCount;
        countdownTrigger = false;

        battleStates = PerformAction.START;
        battlePhases = BattlePhases.FIGHT;

        preFightStarted = false;
    }



    //Actions at the end of turn
    private IEnumerator PostFightActions()
    {
        if (postFightStarted)
        {
            yield break;
        }

        postFightStarted = true;

        CurrentTurn++;

        yield return new WaitForSeconds(0.5f);

        if (EnemiesInBattle.Count > 0)
        {
            for (int i = 0; i < EnemiesInBattle.Count; i++)
            {
                EnemiesInBattle[i].GetComponent<EnemyStateMachine>().RestoreHP(200, 100); //restore 100% of 200HP for each enemy in battle at the end of turn
            }

        }
        //apply all the effects that apply just right before the fight ends (like -HP at the end of the turn)
        //check for status effects and remove 1 tick from them
        // BuffDurationTurns--;
        // DebuffDurationTurns--;
        ChoicesMade = 0;

        //decrease autobattle turns
        if (autoBattle == true)
        {
            AutobattleControl();
        }
        yield return new WaitForSeconds(0.5f);
        //And start a new turn actions
        //HeroInput = HeroGUI.ACTIVATE;
        Time.timeScale = 1;
        Time.fixedDeltaTime = this.fixedDeltaTime * Time.timeScale;

        if (EnemiesInBattle.Count > 0)
            battlePhases = BattlePhases.PLAYERINPUT;

        postFightStarted = false;
    }


    //we will use this later on when we will be using speed buffs
    //and will trigger this method if some will get their speed modified mid-turn
    //for no we will use it at the beginning of each turns
    public void UpdatePerformList()
    {
        PerformList = PerformList.OrderBy(x => x.attackersSpeed).ToList();
        PerformList.Reverse();
    }

    //win / lose / etc
    private IEnumerator PostBattleActions(bool battleWin)
    {
        if (postbattleStarted)
        {
            yield break;
        }

        postbattleStarted = true;

        if (battleWin == true)
        {
            Debug.Log("You won the battle");
            for (int i = 0; i < HeroesInBattle.Count; i++)
            {
                CalculateExp(HeroesInBattle[i].GetComponent<HeroStateMachine>().hero.level.currentlevel);
                HeroesInBattle[i].GetComponent<HeroStateMachine>().currentState = HeroStateMachine.TurnState.WAITING;
                HeroesInBattle[i].GetComponent<HeroStateMachine>().hero.level.AddExp(getExp);
                Debug.Log("Hero " + HeroesInBattle[i].GetComponent<HeroStateMachine>().hero.theName + " gained " + getExp + "EXP.");
            }
        }
        else
        {
            Debug.Log("You lost the battle");
            for (int i = 0; i < HeroesInBattle.Count; i++)
            {
                HeroesInBattle[i].GetComponent<HeroStateMachine>().currentState = HeroStateMachine.TurnState.WAITING;
            }
        }

        //Here goes saving

        yield return new WaitForSeconds(GameManager.instance.postFightCooldown);

        Time.timeScale = 1;
        Time.fixedDeltaTime = this.fixedDeltaTime * Time.timeScale;

        GameManager.instance.LoadSceneAfterBattle();
        GameManager.instance.gameState = GameManager.GameStates.WORLD_STATE;
        GameManager.instance.enemysToBattle.Clear();

        postbattleStarted = false;
    }

    public void SpeedUpTime(float modifier)
    {
        Debug.Log(modifier);
        timeModifier = modifier;
        GameManager.instance.fightSpeed = modifier;
    }

    void CollectExp()
    {
        for (int i = 0; i < EnemiesInBattle.Count; i++)
        {
            xLevel += EnemiesInBattle[i].GetComponent<EnemyStateMachine>().enemy.level.currentlevel;
            expTotal += EnemiesInBattle[i].GetComponent<EnemyStateMachine>().enemy.expAmount;
            averageEnemyLvl = (int)Mathf.Round(xLevel / EnemiesInBattle.Count);
        }
    }

    void CalculateExp(int yLevel)
    {
        Debug.Log("Average level: " + averageEnemyLvl + "expTotal: " + expTotal);
        getExp = (int)Mathf.Round((expTotal * averageEnemyLvl) / (HeroesInBattle.Count * yLevel) * expMultiplier);

    }

}
