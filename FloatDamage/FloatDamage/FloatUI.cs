using Decal.Adapter;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using UtilityBelt.Common.Messages.Events;
using UtilityBelt.Scripting.Interop;
using UtilityBelt.Service;
using UtilityBelt.Service.Lib;
using UtilityBelt.Service.Lib.ACClientModule;
using UtilityBelt.Service.Views;

namespace FloatDamage;
unsafe public class FloatUI : IDisposable
{
    /// <summary>
    /// The UBService Hud
    /// </summary>
    private readonly Hud hud;

    Stopwatch watch = Stopwatch.StartNew();
    Game g = new();
    ACDecalD3D graphics = new();
    List<TimedText> labels = new();

    public FloatUI()
    {
        // Create a new UBService Hud
        hud = UBService.Huds.CreateHud("Hax");

        // set to show our icon in the UBService HudBar
        hud.ShowInBar = true;
        hud.Visible = true;

        hud.WindowSettings = ImGuiWindowFlags.AlwaysAutoResize;

        // subscribe to the hud render event so we can draw some controls
        hud.OnRender += Hud_OnRender; 

        //Update health?
        //g.Messages.Incoming.Combat_QueryHealthResponse += Incoming_Combat_QueryHealthResponse;
        //g.Messages.Incoming.Qualities_PrivateUpdateInt += Incoming_Qualities_PrivateUpdateInt;
        //g.World.OnChatText

        //Update floating text
        g.OnRender2D += G_OnRender2D;

        TimedText.COLOR_A = graphics.Vec4ToColor(new Vector4(255, 255, 255, 1));
        TimedText.COLOR_B = graphics.Vec4ToColor(new Vector4(255, 1, 1, 1));
    }

    private void G_OnRender2D(object sender, EventArgs e)  => UpdateTimedLabels();
    private void G_OnTick(object sender, EventArgs e) => UpdateTimedLabels();

    private void Incoming_Qualities_PrivateUpdateInt(object sender, Qualities_PrivateUpdateInt_S2C_EventArgs e)
    {
    }

    private void UpdateTimedLabels()
    {
        List<TimedText> remove = new();

        for (int i = labels.Count - 1; i >= 0; i--)
        {
            var label = labels[i];
            if (!label.TryUpdate(g.Character.Weenie))
            {
                //Chat($"Removed label after {label.ElapsedSeconds}: {label.Text}");
                labels.Remove(label);
            }
        }
    }

    Dictionary<uint, int> knownHealth = new();
    private void Incoming_Combat_QueryHealthResponse(object sender, Combat_QueryHealthResponse_S2C_EventArgs e)
    {
        //e.Data.ObjectId
        //e.Data.HealthPercent
        var wod = CoreManager.Current.WorldFilter[0];

        if (g.World.TryGet(e.Data.ObjectId, out var wo))
        {
            if (!wo.HasAppraisalData)
                wo.Appraise();

            if (wo.Vitals.TryGetValue(UtilityBelt.Common.Enums.VitalId.Health, out var health))
            {
                //Assume max health
                if (!knownHealth.TryGetValue(e.Data.ObjectId, out var hp))
                {
                    hp = health.Max;
                    knownHealth.AddOrUpdate(e.Data.ObjectId, hp);
                }

                var healthLost = hp - health.Current;
                AddDamage(healthLost);

                Chat($"{wo.Name} hit for {e.Data.HealthPercent:0.00}% | {healthLost} hp");
            }
            else
            {
                Chat($"Unable to find vitals of {wo.Name} - {wo.Vitals.Count} - {wo.Attributes.Count}");
                foreach (var v in wo.Vitals)
                {
                    Chat($"{v.Key}: {v.Value}");
                }
            }
        }
        else
        {
            Chat($"Unable to find {e.Data.ObjectId}");
        }
    }

    void Chat(string message, int color = 1)
    {
        CoreManager.Current.Actions.AddChatText(message, color);
    }  

    //Add a timed damage marker
    private void AddDamage(int amount = 0)
    {
        var s = g.World.Selected;
        if (s is null)
            return;

        if (s.DistanceTo2D(g.Character.Weenie) > TimedText.MAX_DISTANCE)
            return;

        amount = new Random().Next(1000);
        var color = amount > 700 ? TimedText.COLOR_A : TimedText.COLOR_B;

        //Make a marker
        var text = $"{amount}!";
        //var marker = TimedText.TWO_DIMENSIONAL ? graphics.MarkObjectWith2DText(s.Id, text, "Arial", color) : graphics.MarkObjectWith3DText(s.Id, text, "Arial", color);
        var marker = graphics.MarkObjectWith3DText(s.Id, text, "Arial", color);
        marker.Visible = false;
        marker.Color = color;

        TimedText timedText = new(marker, text, s.Id);
        timedText.TryUpdate(g.Character.Weenie);
        marker.Visible = true;

        labels.Add(timedText);
    }

    Vector4 colA = new(0, 0, 0, 1);
    Vector4 colB = new(0, 0, 0, 1);
    private void Hud_OnRender(object sender, EventArgs e)
    {
        if (ImGui.Button("Select"))
        {
            AddDamage();
        }

        ImGui.DragFloat("Alive", ref TimedText.TIME_ALIVE, .2f, .1f, 10f);
        ImGui.DragFloat("Height", ref TimedText.START_HEIGHT, .1f, 0f, 5f);
        ImGui.DragFloat("Delta", ref TimedText.HEIGHT_DELTA, .1f, 1f, 5f);
        ImGui.DragFloat("Distance", ref TimedText.MAX_DISTANCE, 1f, 10f, 200f);
        ImGui.DragFloat("Start Scale", ref TimedText.START_SCALE, .02f, 0.001f, 3f);
        ImGui.DragFloat("End Scale", ref TimedText.END_SCALE, .02f, 0.001f, 3f);
        //ImGui.Checkbox("Fade", ref TimedText.FADE);
        ImGui.Checkbox("Shrink", ref TimedText.SHRINK);
        ImGui.Checkbox("Autoscale", ref TimedText.AUTOSCALE);       //Scales on creature size?
        //ImGui.Checkbox("2D Text", ref TimedText.TWO_DIMENSIONAL);

        if (ImGui.ColorPicker4("Color A", ref colA))
        {
            TimedText.COLOR_A = ImGui.Vec4ToCol(new(colA.Z, colA.Y, colA.X, Math.Max(colB.W, .004f)));
            Chat($"Color set to {TimedText.COLOR_A:X8}");
        }
        if (ImGui.ColorPicker4("Color B", ref colB))
        {
            TimedText.COLOR_B = ImGui.Vec4ToCol(new(colB.Z, colB.Y, colB.X, Math.Max(colB.W, .004f)));
            Chat($"Color set to {TimedText.COLOR_B:X8}");
        }
    }

    public void Dispose()
    {
        try
        {
            hud.OnRender -= Hud_OnRender;

            g.Messages.Incoming.Combat_QueryHealthResponse -= Incoming_Combat_QueryHealthResponse;
            g.OnRender2D -= G_OnRender2D;
            g.OnTick -= G_OnTick;

            graphics?.Dispose();
            hud.Dispose();

            //hud.OnPreRender += Hud_OnPreRender;

            //g.Messages.Incoming.Message += Incoming_Message;
            //g.Messages.Outgoing.Message += Outgoing_Message;
        }
        catch (Exception ex)
        {
            PluginCore.Log(ex);
        }
    }


    public class TimedText
    {
        public static float TIME_ALIVE = .5f;
        public static float START_HEIGHT = 1;
        public static float HEIGHT_DELTA = 1;
        public static float MAX_DISTANCE = 50;
        public static float END_SCALE = 1.5f;
        public static float START_SCALE = 0.5f;
        float ScaleDelta => END_SCALE - START_SCALE;

        public static uint COLOR_A = 0x01FF0000;
        public static uint COLOR_B = 0x0100FF00;

        public static bool FADE = false;
        public static bool SHRINK = true;
        public static bool AUTOSCALE = false;
        //public static bool TWO_DIMENSIONAL = false;

        public DateTime Start = DateTime.Now;
        public double ElapsedSeconds => (DateTime.Now - Start).TotalSeconds;
        public float PercentDone => (float)(ElapsedSeconds / TIME_ALIVE);
        public DecalD3DObj Marker;
        public string Text = "";
        public WorldObject Obj;

        public TimedText(DecalD3DObj marker, string text, WorldObject obj)
        {
            //Start = start;
            Marker = marker;
            Text = text;
            Obj = obj;
        }

        public bool TryUpdate(WorldObject player)
        {
            try
            {
                //Check WorldObject / time elapsed / distance
                if (Obj is null ||
                    ElapsedSeconds > TIME_ALIVE ||
                    Obj.DistanceTo2D(player) > MAX_DISTANCE
                    )
                {
                    Marker?.Dispose();
                    return false;
                }

                //Adjust
                Marker.Anchor(Obj.Id, START_HEIGHT + HEIGHT_DELTA * PercentDone, 0, 0, 0);

                if (SHRINK)
                    //Marker.Scale(1 - (PercentDone * END_SCALE));
                    Marker.Scale(START_SCALE + ScaleDelta * PercentDone);

                Marker.Autoscale = AUTOSCALE;
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}
