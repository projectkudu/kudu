function myFunction() {
    var hawk = new person("hawk", "foreste", 23);
    hawk.changeAge(24);
    document.write("working");
}

function person(firstname, lastname, age) {
    this.firstName = firstname;
    this.lastName = lastname;
    this.age = age;

    function changeAge(newAge) {
        this.age = newAge;
    }
}