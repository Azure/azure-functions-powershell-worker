class Function : Attribute {
    [string]$Name
    Function() {
    }
    
    Function([string]$Name) {
        $this.Name = $Name
    }
}

class HttpTrigger : Attribute {
    [string]$AuthLevel
    [string[]]$Methods

    HttpTrigger() {
    }
    
    HttpTrigger([string]$AuthLevel) {
        $this.AuthLevel = $AuthLevel
    }
    
    HttpTrigger([string]$AuthLevel, [string[]]$Methods) {
        $this.AuthLevel = $AuthLevel
        $this.Methods = $Methods
    }
}

class TimerTrigger : Attribute { 
    [string]$chron

    TimerTrigger() {
    }

    TimerTrigger([string]$chron) {
        $this.chron = $chron
    }
}

class EventGridTrigger : Attribute { 
    EventGridTrigger() {
    }
}
