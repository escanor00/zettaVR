using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Âncoras espaciais persistentes (AR Foundation + Meta OpenXR): os hologramas criados são salvos
// e reaparecem no mesmo lugar ao reabrir o app.
public class AncorasHolo : MonoBehaviour
{
    public float intervaloVarredura = 1.5f;
    public float movimentoMinimo = 0.03f;   // m
    public float rotacaoMinima = 5f;        // graus
    public float segundosParado = 2f;

    // fábricas por tipo: recriam o objeto numa pose do mundo a partir do que foi salvo
    public static readonly Dictionary<string, Func<HoloItem, Vector3, Quaternion, GameObject>> Fabricas =
        new Dictionary<string, Func<HoloItem, Vector3, Quaternion, GameObject>>();

    [Serializable]
    class Reg
    {
        public string guid, tipo, forma, cor, extra, estado;
        public float tamanho;
    }

    class Estado
    {
        public Reg reg;
        public Vector3 posSalva, posAnterior;
        public Quaternion rotSalva;
        public float paradoDesde;
        public bool ocupado;
    }

    ARAnchorManager mgr;
    readonly Dictionary<GameObject, Estado> estados = new Dictionary<GameObject, Estado>();
    readonly List<Reg> salvos = new List<Reg>();
    string arquivo;
    float proxima;
    bool pronto;
    public int QuantidadeSalvos => salvos.Count;
    public bool Pronto => pronto;

    void Awake()
    {
        arquivo = Path.Combine(Application.persistentDataPath, "HologramasAncorados.json");
        HoloRegistro.aoRemover += AoRemover;
    }

    void OnDestroy() { HoloRegistro.aoRemover -= AoRemover; }

    IEnumerator Start()
    {
        mgr = FindAnyObjectByType<ARAnchorManager>();
        if (mgr == null) yield break;
        // espera o subsistema de âncoras (e o rastreamento) ficarem prontos
        float limite = Time.time + 40f;
        while ((mgr.subsystem == null || !mgr.subsystem.running) && Time.time < limite) yield return new WaitForSeconds(0.5f);
        if (mgr.subsystem == null || !mgr.subsystem.running) yield break;
        var d = mgr.descriptor;
        if (d == null || !d.supportsSaveAnchor || !d.supportsLoadAnchor || !d.supportsEraseAnchor)
        {
            Debug.Log("AncorasHolo: este dispositivo/provedor não suporta âncoras persistentes; recurso desligado.");
            yield break;
        }
        yield return new WaitForSeconds(2f);

        LerArquivo();
        pronto = true;
        yield return CarregarTodos();
    }

    void LerArquivo()
    {
        try
        {
            if (File.Exists(arquivo))
                salvos.AddRange(JsonConvert.DeserializeObject<List<Reg>>(File.ReadAllText(arquivo)) ?? new List<Reg>());
        }
        catch (Exception e) { Debug.LogWarning("AncorasHolo: não consegui ler o arquivo: " + e.Message); }
    }

    void GravarArquivo()
    {
        try { File.WriteAllText(arquivo, JsonConvert.SerializeObject(salvos, Formatting.Indented)); }
        catch (Exception e) { Debug.LogWarning("AncorasHolo: não consegui gravar: " + e.Message); }
    }

    static SerializableGuid ParseGuid(string s)
    {
        var t = s.Split('-');
        return new SerializableGuid(Convert.ToUInt64(t[0], 16), Convert.ToUInt64(t[1], 16));
    }

    IEnumerator CarregarTodos()
    {
        foreach (var reg in new List<Reg>(salvos))
        {
            var tarefa = CarregarUm(reg);
            while (!tarefa.IsCompleted) yield return null;
        }
    }

    async Awaitable CarregarUm(Reg reg)
    {
        try
        {
            var r = await mgr.TryLoadAnchorAsync(ParseGuid(reg.guid));
            if (r.status.IsError())
            {
                Debug.LogWarning("AncorasHolo: âncora não carregada (" + r.status.statusCode + ")");
                return;
            }
            var anchor = r.value;
            var pos = anchor.transform.position;
            var rot = anchor.transform.rotation;
            if (!Fabricas.TryGetValue(reg.tipo, out var fabrica)) { mgr.TryRemoveAnchor(anchor); return; }

            HoloRegistro.Carregando = true;
            GameObject go;
            try
            {
                go = fabrica(new HoloItem { tipo = reg.tipo, forma = reg.forma, cor = reg.cor, extra = reg.extra, tamanho = reg.tamanho, estado = reg.estado }, pos, rot);
                if (go != null && !string.IsNullOrEmpty(reg.estado)) ObjetoInterativo.Garantir(go).AplicarEstado(reg.estado);
            }
            finally { HoloRegistro.Carregando = false; }

            mgr.TryRemoveAnchor(anchor); // a pose já foi lida; o dado persistido continua salvo
            if (go != null)
                estados[go] = new Estado { reg = reg, posSalva = pos, rotSalva = rot, posAnterior = pos, paradoDesde = Time.time };
        }
        catch (Exception e) { Debug.LogWarning("AncorasHolo: erro ao carregar: " + e.Message); }
    }

    void Update()
    {
        if (!pronto || Time.time < proxima) return;
        proxima = Time.time + intervaloVarredura;

        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null) continue;
            if (it.go.GetComponent<SeguirMao>() != null) continue; // preso ao braço: não re-ancora
            if (!Fabricas.ContainsKey(it.tipo)) continue;           // sem fábrica não dá para recriar: não ancora
            var t = it.go.transform;
            if (!estados.TryGetValue(it.go, out var e))
            {
                e = new Estado
                {
                    reg = new Reg { tipo = it.tipo, forma = it.forma, cor = it.cor, extra = it.extra, tamanho = it.tamanho, estado = EstadoDe(it.go) },
                    posAnterior = t.position,
                    posSalva = new Vector3(float.NaN, 0, 0),
                    rotSalva = t.rotation,
                    paradoDesde = Time.time
                };
                estados[it.go] = e;
            }
            if (e.ocupado) continue;

            // partes pintadas, escondidas, separadas ou girando mudaram: salva de novo com a âncora
            string est = EstadoDe(it.go);
            if (est != (e.reg.estado ?? "")) { e.reg.estado = est; e.posSalva = new Vector3(float.NaN, 0, 0); }

            if (Vector3.Distance(t.position, e.posAnterior) > 0.005f) { e.paradoDesde = Time.time; e.posAnterior = t.position; }
            bool segurando = false;
            var g = it.go.GetComponent<XRGrabInteractable>();
            if (g != null) segurando = g.isSelected;
            if (segurando) { e.paradoDesde = Time.time; continue; }
            if (Time.time - e.paradoDesde < segundosParado) continue;

            bool nuncaSalvo = float.IsNaN(e.posSalva.x);
            bool mudou = !nuncaSalvo && (Vector3.Distance(t.position, e.posSalva) > movimentoMinimo
                || Quaternion.Angle(t.rotation, e.rotSalva) > rotacaoMinima);
            if (nuncaSalvo || mudou) _ = SalvarAsync(it.go, e);
        }
    }

    async Awaitable SalvarAsync(GameObject go, Estado e)
    {
        e.ocupado = true;
        try
        {
            var pos = go.transform.position;
            var rot = go.transform.rotation;
            var add = await mgr.TryAddAnchorAsync(new Pose(pos, rot));
            if (add.status.IsError()) return;
            var anchor = add.value;
            var salvo = await mgr.TrySaveAnchorAsync(anchor);
            mgr.TryRemoveAnchor(anchor);
            if (salvo.status.IsError()) { Debug.LogWarning("AncorasHolo: falha ao salvar âncora (" + salvo.status.statusCode + ")"); return; }

            string antigo = e.reg.guid;
            e.reg.guid = salvo.value.ToString();
            e.posSalva = pos;
            e.rotSalva = rot;
            if (!salvos.Contains(e.reg)) salvos.Add(e.reg);
            GravarArquivo();
            if (!string.IsNullOrEmpty(antigo)) await mgr.TryEraseAnchorAsync(ParseGuid(antigo));
        }
        catch (Exception ex) { Debug.LogWarning("AncorasHolo: erro ao salvar: " + ex.Message); }
        finally { e.ocupado = false; }
    }

    static string EstadoDe(GameObject go)
    {
        var oi = go != null ? go.GetComponent<ObjetoInterativo>() : null;
        return oi != null ? oi.Serializar() : "";
    }

    // cor/tamanho mudaram: atualiza o registro salvo e força nova gravação no próximo ciclo
    public void MarcarAlterado(GameObject go)
    {
        var it = HoloRegistro.Buscar(go);
        if (it == null || !estados.TryGetValue(go, out var e)) return;
        e.reg.cor = it.cor;
        e.reg.tamanho = it.tamanho;
        e.reg.forma = it.forma;
        e.reg.extra = it.extra;
        e.reg.estado = EstadoDe(go);
        e.posSalva = new Vector3(float.NaN, 0, 0);
    }

    void AoRemover(GameObject go)
    {
        if (go == null || !estados.TryGetValue(go, out var e)) return;
        estados.Remove(go);
        if (salvos.Remove(e.reg))
        {
            GravarArquivo();
            if (!string.IsNullOrEmpty(e.reg.guid) && mgr != null) _ = ApagarAncora(e.reg.guid);
        }
    }

    async Awaitable ApagarAncora(string guid)
    {
        try { await mgr.TryEraseAnchorAsync(ParseGuid(guid)); }
        catch (Exception ex) { Debug.LogWarning("AncorasHolo: erro ao apagar âncora: " + ex.Message); }
    }

    // apaga tudo (usado por "apagar tudo")
    public void ApagarTodasAncoras()
    {
        foreach (var reg in new List<Reg>(salvos))
            if (!string.IsNullOrEmpty(reg.guid) && mgr != null) _ = ApagarAncora(reg.guid);
        salvos.Clear();
        estados.Clear();
        GravarArquivo();
    }
}
