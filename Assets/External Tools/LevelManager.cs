using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;//正则表达式命名空间
using UnityEngine;

//------------------------------------------------------------
// 作者名：一冰在北
// GitHub项目地址：https://github.com/IceinNorth/Ice-Game-Development-Toolkit
// 邮箱：wu2687180662@icloud.com
//------------------------------------------------------------

public class LevelManager : MonoBehaviour
{
    public Transform PlayerTr;
    public Camera Camera;
    public GameObject ActiveLevel; //启用的关卡
    private Action ActiveAction; //关卡切换事件
    private List<GameObject> loadedLevelList; //已加载关卡列表
    private GameObject[] levels; //文件中的所有关卡

    private Vector2Int oldIndex; //上一个关卡索引：用于防止切换后上一个关卡被卸载
    private Vector2Int nowIndex; //以坐标为形式的当前关卡索引
    public Vector2Int NowIndex
    {
        get
        {
            return nowIndex;
        }
    }
    private bool IsNotVisableInCamera; //玩家是否离开视线范围

    public bool Asynchronous; //开启预加载

    void OnEnable()
    {
        //事件订阅
        ActiveAction += UnloadLevels;
        ActiveAction += Asynchronous? LoadLevels : null; //根据Asynchronous判断是否采用预加载
        ActiveAction += SetLevelActive;
    }

    void Start()
    {
        //DontDestroyOnLoad(this); //非必须
        //实例化
        Camera =Camera == null? Camera.main:Camera; //获取主摄像机，也可以通过Inspector面板拖拽获取
        PlayerTr = PlayerTr == null? GameObject.FindWithTag("Player").transform:PlayerTr; //这里使用名为“Player”的标签来获取玩家的transform组件，也可以通过Inspector面板拖拽获取
        ActiveLevel = ActiveLevel == null? GameObject.FindWithTag("Levels"):ActiveLevel; //这里会获取场景中标签为“Level”的物体，也可以通过Inspector面板拖拽获取
        levels = Resources.LoadAll<GameObject>("Levels"); //需要在Assets中创建Resource文件夹并在其中创建关卡文件夹，关卡文件夹名字自定，这里使用“Levels”
        loadedLevelList = new List<GameObject>();
        //初始化
        loadedLevelList.Add(ActiveLevel); //列表添加当前关卡
        nowIndex = GetIndex(ActiveLevel); //获取当前关卡索引
        ActiveAction(); //初始化
    }
    
    void Update()
    {
        nowIndex = SwitchPlayer(PlayerTr,nowIndex); //更新当前索引
        if (IsNotVisableInCamera)
        {
            //玩家离开视线后进行切换事件
            ActiveAction();
        }
    }

    private Vector2Int SwitchPlayer(Transform transform,Vector2Int index)
    {
        //当玩家离开视线后返回新索引
        Vector3 pos = transform.position; //获取玩家坐标
        Vector3 viewPos = Camera.WorldToViewportPoint(pos); //玩家坐标从世界空间转换为视图空间
        //视线内的坐标的xy值会被转换为0-1之间的数值，超出视线则xy值不在此范围，借此来判断玩家离开视线的方向
        if (viewPos.x < 0)
        {
            //当玩家向左离开时
            index.x -= 1; //索引坐标更新
            pos.x =-pos.x-0.3f; //玩家被移动到视线的另一端，此处的-0.2f是为了防止玩家在移到视线另一端后仍在视线外而造成关卡再次切换，此值偏小会导致关卡反复切换
            IsNotVisableInCamera = true; //更新布尔值来在update中调用切换事件
        }
        //以下同理
        else if (viewPos.x > 1)
        {
            index.x += 1;
            pos.x = -pos.x+0.3f;
            IsNotVisableInCamera = true;
        } 
        else if (viewPos.y < 0)
        {
            index.y -= 1;
            pos.y =-pos.y-0.3f;
            IsNotVisableInCamera = true;
        }
        else if (viewPos.y > 1)
        {
            index.y += 1;
            pos.y = -pos.y+1.5f; //玩家向上移动时的偏移量需要比其他方向上更大才能防止关卡反复切换，原因未知
            IsNotVisableInCamera = true;
        }
        else
        {
            //玩家未离开视线
            IsNotVisableInCamera = false;
        }
        transform.position = pos; //坐标在函数内部更新后再赋给position
        return index; //返回更新后的索引
    }

    
    private Vector2Int GetIndex(GameObject Level)
    {
        //根据关卡预制体返回索引
        Vector2Int index = Vector2Int.zero; //声明一个临时索引
        bool xChanged = false;//用于判断x是否被改变
        Regex reg = new Regex("-?[0-9]+", RegexOptions.IgnoreCase | RegexOptions.Singleline); //声明一个正则表达式，该表达式只会提取文本中的数字
        MatchCollection mc = reg.Matches(Level.name); //通过表达式来提取关卡名中的数字，如果关卡名为”2，3 示例关卡“，进行提取后就会产生包含2和3的集合
        
        foreach (Match m in mc)
        {
            if (!xChanged) 
            {
                index.x = int.Parse(m.Groups[0].Value);
                xChanged = true;
            }
            else
            {
                index.y = int.Parse(m.Groups[0].Value);
            }
        } 
        return index; //返回得到的索引
    }

    private IEnumerator IAsyLoad()
    {
        //异步加载协程
        //预加载四个方向上的关卡
        yield return null; //每隔一帧加载一个关卡
        Load(nowIndex+Vector2Int.up,false);
        yield return null;
        Load(nowIndex+Vector2Int.down,false);
        yield return null;
        Load(nowIndex+Vector2Int.left,false);
        yield return null;
        Load(nowIndex+Vector2Int.right,false);
    }
    
    private void LoadLevels()
    {
        StartCoroutine(IAsyLoad());
    }

    private void UnloadLevels()
    {
        //卸载关卡
        for (int i = loadedLevelList.Count-1; i >= 0; i--)
        {
            //这里需要遍历列表并按条件移除关卡，采用倒历的方法来遍历列表
            GameObject level = loadedLevelList[i];
            Vector2Int index = GetIndex(level);
            if (index != GetIndex(ActiveLevel) && index != nowIndex)
            {
                //如果该关卡不是上一个关卡和当前关卡便移除
                loadedLevelList.Remove(level);
                Destroy(level);
            }
        }
    }
    
    private void Load(Vector2Int index,bool signal)
    {
        //加载关卡
        bool levelInList = false; //该布尔值用于判断关卡是否已经加载
        string name = index.x + "," + index.y; //索引转换为字符串
        foreach (GameObject i in loadedLevelList) 
        {
            //遍历已加载的关卡，判断将要加载关卡是否已经加载
            if (i.name == name)
            {
                levelInList = true;
                i.SetActive(signal);
                ActiveLevel = signal? i: ActiveLevel;
            }
        }

        if (!levelInList)
        {
            foreach (GameObject i in levels)
            {
                //遍历所有关卡
                if (index == GetIndex(i))
                {
                    GameObject level = Instantiate(i); //将获取到的关卡载入场景中
                    level.name = name; //载入关卡后名字改为索引，方便调用
                    level.SetActive(signal); //关卡状态
                    ActiveLevel = signal? i: ActiveLevel; 
                    loadedLevelList.Add(level); //该关卡添加进入已加载关卡列表
                    break;
                }
            }
        }
    }
    private void SetLevelActive()
    {
        //启用关卡
        foreach (GameObject i in loadedLevelList)
        {
            i.SetActive(false);
        }
        Load(nowIndex,true);
    }
}
