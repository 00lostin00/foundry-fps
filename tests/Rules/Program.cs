using Foundry;

int checks=0;
void Require(bool value,string message) {checks++;if(!value)throw new Exception(message);}
foreach(var spec in WeaponRules.All) {
    for(int magazine=0;magazine<=spec.Magazine;magazine++)for(int reserve=0;reserve<=spec.Reserve;reserve++) {
        int transferred=WeaponRules.ReloadAmount(magazine,reserve,spec.Magazine);
        Require(transferred>=0 && transferred<=reserve,"Reserve underflow");
        Require(magazine+transferred<=spec.Magazine,"Magazine overflow");
        Require((magazine+transferred)+(reserve-transferred)==magazine+reserve,"Ammo conservation");
    }
    Require(!WeaponRules.CanFire(0,1,0,false,true),"Empty magazine fired");
    Require(!WeaponRules.CanFire(1,1,2,false,true),"Fire rate bypassed");
    Require(!WeaponRules.CanFire(1,2,1,true,true),"Fired while reloading");
    Require(!WeaponRules.CanFire(1,2,1,false,false),"Dead player fired");
    Require(WeaponRules.CanFire(1,2,2,false,true),"Valid shot rejected");
    Require(WeaponRules.Damage(spec.Damage,true)==2*spec.Damage,"Headshot multiplier");
}
Console.WriteLine($"PASS: {checks} weapon rule assertions");
